# MCP Launcher

MCP Launcher is an Avalonia desktop application for starting and inspecting local MCP server processes without keeping a separate terminal window open for each one.

The current codebase implements the first product-plan workflows: an externally edited server catalog, manual refresh, process lifecycle controls, current-session run logs, configuration error handling, and safe shutdown prompts. See [PRODUCT_PLAN.md](PRODUCT_PLAN.md) for the product decisions and validation plan.

## Prerequisites

- .NET 8 SDK
- Windows as the primary platform, or macOS for validation

## Run

```shell
dotnet restore MCPLauncher.sln
dotnet run --project src/MCPLauncher/MCPLauncher.csproj
```

MCP Launcher creates `servers.json` under the current user's local application-data directory the first time it runs. Use **Open configuration** in the application to open the exact file.

## Configure servers

Entries live under a `servers` object and use a stable, human-readable key. `command` is required; all other fields shown below are optional.

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

MCP Launcher does not install MCP servers, configure MCP clients, discover external processes, validate MCP readiness, or persist logs.
