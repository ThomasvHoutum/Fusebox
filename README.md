# Fusebox

Fusebox is an Avalonia desktop application for starting and inspecting local MCP server processes without keeping a separate terminal window open for each one.

The current codebase implements the first product-plan workflows: an externally edited server catalog, manual refresh, process lifecycle controls, current-session run logs, configuration error handling, and safe shutdown prompts.

## Prerequisites

- .NET 10 SDK
- Windows as the primary platform, or macOS for validation

## Run

```shell
dotnet restore Fusebox.sln
dotnet run --project src/Fusebox/Fusebox.csproj
```

Fusebox creates `servers.json` under the current user's local application-data directory the first time it runs. Use **Open configuration** in the application to open the exact file.

## Configure servers

Entries live under a `servers` object and use a stable, human-readable key. `command` is required; all other fields shown below are optional.

`command` can be an executable name, a full executable path, or a complete command
line. Using `args` for the arguments is preferred because it avoids quoting
ambiguity. Executable names registered globally can be used without a path; the
launcher inherits the user's `PATH` in both debug and published builds.

Paths containing spaces are supported. When a complete command line starts with
such a path, quote it, for example:

```json
{
  "command": "\"C:\\Program Files\\Example MCP\\server.exe\" --serve"
}
```

Commands are parsed directly and are not run through a shell, so shell operators
such as `&&`, pipes, and output redirection are not supported.

```json
{
  "servers": {
    "example-mcp": {
      "title": "Example MCP",
      "description": "A locally hosted MCP server",
      "command": "path-to-command",
      "args": ["--serve"],
      "env": {
        "EXAMPLE_API_KEY": "replace-me"
      },
      "port": 3000
    }
  }
}
```

After saving the file, select **Refresh**. Changes to a running entry apply on its next restart. Start and Restart reread the file before launching, and Restart validates the newest definition before stopping the existing process.

Environment values are stored as plaintext. Keep the configuration file out of source control, shared folders, and inappropriate backups.

## Current scope

- Start, stop, and restart one configured process at a time.
- Show Stopped, Starting, Running, Stopping, and Failed process states.
- Keep output in separate runs for the current application session.
- Preserve the last successfully loaded catalog when the JSON file is unreadable.
- Stop managed processes when the application closes, with confirmation and force-quit escalation.

Fusebox does not install MCP servers, configure MCP clients, discover external processes, validate MCP readiness, or persist logs.

## Architecture

The application follows classic MVVM boundaries:

- `Models` contains configuration records and observable runtime/session state.
- `ViewModels` owns presentation state and coordinates user workflows through injected abstractions.
- `Views` contains AXAML, dialogs, and the minimal window-lifecycle forwarding that cannot be expressed as a normal command.
- `Commands` adapts asynchronous view-model operations to `ICommand` for AXAML bindings.
- `Services` gives each external concern a focused implementation: configuration-file storage, JSON parsing, process supervision, platform file opening, and user interaction.

`App` is the composition root. It creates the concrete services, injects them into `MainWindowViewModel`, and then assigns that view model to the window. The view does not parse configuration, manage processes, or implement application workflows.

## License

Fusebox is available under the [MIT License](LICENSE).
