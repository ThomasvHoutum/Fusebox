using System.Diagnostics;
using System.Text;
using Avalonia.Threading;
using Fusebox.Models;

namespace Fusebox.Services;

public sealed class ManagedProcessService : IManagedProcessService
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(3);

    public void Start(ServerRuntime server, ServerDefinition definition)
    {
        server.State = ServerState.Starting;
        server.ConfigurationAppliedToProcess();
        var run = server.BeginRun();
        var launch = ResolveLaunch(definition);
        run.AppendLifecycle($"Starting {FormatCommand(launch.FileName, launch.Arguments)}.");

        var startInfo = new ProcessStartInfo
        {
            FileName = launch.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in launch.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in definition.Environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                Dispatcher.UIThread.Post(() => run.AppendStandardOutput(eventArgs.Data));
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                Dispatcher.UIThread.Post(() => run.AppendStandardError(eventArgs.Data));
            }
        };
        process.Exited += (_, _) => Dispatcher.UIThread.Post(() => HandleExited(server, process, run));

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The operating system did not create the process.");
            }

            server.Process = process;
            server.ExitWasRequested = false;
            server.State = ServerState.Running;
            run.AppendLifecycle($"Process started with ID {process.Id}. Running indicates process state only, not MCP readiness.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception exception)
        {
            process.Dispose();
            server.Process = null;
            server.State = ServerState.Failed;
            run.AppendLifecycle($"Launch failed: {exception.Message}");
            run.Complete("Launch failed.");
        }
    }

    private static (string FileName, IReadOnlyList<string> Arguments) ResolveLaunch(ServerDefinition definition)
    {
        // An explicit args array is unambiguous. An existing path must also remain
        // intact because executable paths can legitimately contain spaces.
        if (definition.Arguments.Count > 0 ||
            !definition.Command.Any(char.IsWhiteSpace) ||
            File.Exists(definition.Command))
        {
            return (definition.Command, definition.Arguments);
        }

        var parts = SplitCommandLine(definition.Command);
        return parts.Count > 0
            ? (parts[0], parts.Skip(1).ToArray())
            : (definition.Command, definition.Arguments);
    }

    private static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        var partStarted = false;
        char? quote = null;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (quote is not null)
            {
                if (character == quote)
                {
                    quote = null;
                }
                else if (character == '\\' &&
                         index + 1 < commandLine.Length &&
                         commandLine[index + 1] == quote)
                {
                    part.Append(commandLine[++index]);
                }
                else
                {
                    part.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                partStarted = true;
                quote = character;
            }
            else if (char.IsWhiteSpace(character))
            {
                AddPart(parts, part, ref partStarted);
            }
            else
            {
                partStarted = true;
                part.Append(character);
            }
        }

        if (quote is not null)
        {
            return [];
        }

        AddPart(parts, part, ref partStarted);
        return parts;
    }

    private static void AddPart(ICollection<string> parts, StringBuilder part, ref bool partStarted)
    {
        if (!partStarted)
        {
            return;
        }

        parts.Add(part.ToString());
        part.Clear();
        partStarted = false;
    }

    private static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
    {
        var formattedFileName = fileName.Any(char.IsWhiteSpace) ? $"\"{fileName}\"" : fileName;
        return arguments.Count == 0
            ? formattedFileName
            : $"{formattedFileName} {string.Join(" ", arguments.Select(FormatArgument))}";
    }

    private static string FormatArgument(string argument) =>
        argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"")}\"" : argument;

    public async Task<StopResult> StopAsync(ServerRuntime server, string endReason)
    {
        var process = server.Process;
        if (process is null || HasExited(process))
        {
            return StopResult.NotRunning;
        }

        server.ExitWasRequested = true;
        server.RequestedEndReason = endReason;
        server.State = ServerState.Stopping;
        server.CurrentRun?.AppendLifecycle("Termination requested.");

        try
        {
            // A direct process termination is the only portable baseline for arbitrary
            // commands. Force quit escalates to the complete descendant process tree.
            process.Kill(entireProcessTree: false);
            using var timeout = new CancellationTokenSource(TerminationTimeout);
            await process.WaitForExitAsync(timeout.Token);
            CompleteRequestedExit(server, process, server.CurrentRun);
            return StopResult.Stopped;
        }
        catch (OperationCanceledException)
        {
            server.State = ServerState.Running;
            server.CurrentRun?.AppendLifecycle("The process did not stop. Force quit or cancel.");
            return StopResult.RequiresForce;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            server.State = ServerState.Running;
            server.CurrentRun?.AppendLifecycle($"The process did not stop: {exception.Message}");
            return StopResult.RequiresForce;
        }
    }

    public async Task<bool> ForceQuitAsync(ServerRuntime server, string endReason)
    {
        var process = server.Process;
        if (process is null || HasExited(process))
        {
            return true;
        }

        server.ExitWasRequested = true;
        server.RequestedEndReason = endReason;
        server.State = ServerState.Stopping;
        server.CurrentRun?.AppendLifecycle("Force quit requested.");

        try
        {
            process.Kill(entireProcessTree: true);
            using var timeout = new CancellationTokenSource(TerminationTimeout);
            await process.WaitForExitAsync(timeout.Token);
            CompleteRequestedExit(server, process, server.CurrentRun);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            server.State = ServerState.Running;
            server.CurrentRun?.AppendLifecycle($"Force quit failed: {exception.Message}");
            return false;
        }
    }

    private static void HandleExited(ServerRuntime server, Process process, ServerRun run)
    {
        if (!ReferenceEquals(server.Process, process))
        {
            process.Dispose();
            return;
        }

        if (server.ExitWasRequested)
        {
            CompleteRequestedExit(server, process, run);
            return;
        }

        var exitCode = TryGetExitCode(process);
        server.Process = null;
        server.State = ServerState.Failed;
        run.Complete(exitCode is int code
            ? $"Process exited unexpectedly with code {code}."
            : "Process exited unexpectedly.");
        process.Dispose();
    }

    private static void CompleteRequestedExit(ServerRuntime server, Process process, ServerRun? run)
    {
        if (!ReferenceEquals(server.Process, process))
        {
            return;
        }

        server.Process = null;
        server.ExitWasRequested = false;
        server.State = ServerState.Stopped;
        run?.Complete(server.RequestedEndReason);
        process.Dispose();
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
