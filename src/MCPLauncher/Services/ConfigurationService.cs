using System.Diagnostics;
using System.Text.Json;
using MCPLauncher.Models;

namespace MCPLauncher.Services;

public sealed class ConfigurationService
{
    private const string ConfigurationFileName = "servers.json";

    public ConfigurationService(string? applicationDataDirectory = null)
    {
        var root = applicationDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ConfigurationDirectory = Path.Combine(root, "MCPLauncher");
        ConfigurationPath = Path.Combine(ConfigurationDirectory, ConfigurationFileName);
    }

    public string ConfigurationDirectory { get; }

    public string ConfigurationPath { get; }

    public async Task<bool> EnsureConfigurationExistsAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ConfigurationPath))
        {
            return false;
        }

        Directory.CreateDirectory(ConfigurationDirectory);
        await File.WriteAllTextAsync(
            ConfigurationPath,
            "{\n  \"servers\": {}\n}\n",
            cancellationToken);
        return true;
    }

    public void OpenConfigurationFile()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = ConfigurationPath,
            UseShellExecute = true
        });
    }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigurationPath))
        {
            return Unreadable($"Configuration file was not found at {ConfigurationPath}.");
        }

        try
        {
            await using var stream = File.OpenRead(ConfigurationPath);
            using var document = await JsonDocument.ParseAsync(
                stream,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip },
                cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Unreadable("The configuration root must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Object)
            {
                return Unreadable("The configuration must contain a 'servers' object.");
            }

            return ParseEntries(servers);
        }
        catch (JsonException exception)
        {
            return Unreadable($"The configuration contains invalid JSON: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unreadable($"The configuration could not be read: {exception.Message}");
        }
    }

    private static ConfigurationLoadResult ParseEntries(JsonElement servers)
    {
        var properties = servers.EnumerateObject().ToList();
        var duplicateIds = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var entries = new List<ConfigurationEntry>();
        foreach (var property in properties)
        {
            var id = property.Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                entries.Add(new ConfigurationEntry("<empty key>", null, "The server key cannot be empty."));
                continue;
            }

            if (duplicateIds.Contains(id))
            {
                entries.Add(new ConfigurationEntry(id, null, $"The key '{id}' is duplicated. Every server key must be unique."));
                continue;
            }

            entries.Add(ParseEntry(id, property.Value));
        }

        return new ConfigurationLoadResult(true, entries, []);
    }

    private static ConfigurationEntry ParseEntry(string id, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid(id, "The entry must be a JSON object.");
        }

        var titleResult = ReadOptionalString(element, "title");
        if (titleResult.Error is not null)
        {
            return Invalid(id, titleResult.Error);
        }

        var descriptionResult = ReadOptionalString(element, "description");
        if (descriptionResult.Error is not null)
        {
            return Invalid(id, descriptionResult.Error);
        }

        var commandResult = ReadRequiredString(element, "command");
        if (commandResult.Error is not null)
        {
            return Invalid(id, commandResult.Error);
        }

        var argumentsResult = ReadArguments(element);
        if (argumentsResult.Error is not null)
        {
            return Invalid(id, argumentsResult.Error);
        }

        var environmentResult = ReadEnvironment(element);
        if (environmentResult.Error is not null)
        {
            return Invalid(id, environmentResult.Error);
        }

        var portResult = ReadPort(element);
        if (portResult.Error is not null)
        {
            return Invalid(id, portResult.Error);
        }

        var definition = new ServerDefinition(
            id,
            titleResult.Value ?? id,
            descriptionResult.Value ?? string.Empty,
            commandResult.Value!,
            argumentsResult.Value!,
            environmentResult.Value!,
            portResult.Value);

        return new ConfigurationEntry(id, definition, null);
    }

    private static (string? Value, string? Error) ReadRequiredString(JsonElement element, string name)
    {
        var result = ReadOptionalString(element, name);
        if (result.Error is not null)
        {
            return result;
        }

        return string.IsNullOrWhiteSpace(result.Value)
            ? (null, $"'{name}' is required and cannot be empty.")
            : result;
    }

    private static (string? Value, string? Error) ReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return (null, null);
        }

        return property.ValueKind == JsonValueKind.String
            ? (property.GetString(), null)
            : (null, $"'{name}' must be a string.");
    }

    private static (IReadOnlyList<string>? Value, string? Error) ReadArguments(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var property))
        {
            return ([], null);
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return (null, "'args' must be an array of strings.");
        }

        var arguments = new List<string>();
        foreach (var argument in property.EnumerateArray())
        {
            if (argument.ValueKind != JsonValueKind.String)
            {
                return (null, "Every value in 'args' must be a string.");
            }

            arguments.Add(argument.GetString()!);
        }

        return (arguments, null);
    }

    private static (IReadOnlyDictionary<string, string>? Value, string? Error) ReadEnvironment(JsonElement element)
    {
        if (!element.TryGetProperty("env", out var property))
        {
            return (new Dictionary<string, string>(), null);
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return (null, "'env' must be an object containing string values.");
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in property.EnumerateObject())
        {
            if (variable.Value.ValueKind != JsonValueKind.String)
            {
                return (null, $"Environment value '{variable.Name}' must be a string.");
            }

            environment[variable.Name] = variable.Value.GetString()!;
        }

        return (environment, null);
    }

    private static (int? Value, string? Error) ReadPort(JsonElement element)
    {
        if (!element.TryGetProperty("port", out var property))
        {
            return (null, null);
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var port) || port is < 1 or > 65535)
        {
            return (null, "'port' must be an integer from 1 through 65535.");
        }

        return (port, null);
    }

    private static ConfigurationEntry Invalid(string id, string error) => new(id, null, error);

    private static ConfigurationLoadResult Unreadable(string error) => new(false, [], [error]);
}
