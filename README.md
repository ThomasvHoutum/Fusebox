# Fusebox

Fusebox is a desktop application for starting, stopping, and inspecting local MCP
server processes without keeping a separate terminal window open for each one. It
provides an externally editable server catalog, process lifecycle controls, live
output grouped by run, configuration error handling, and safe shutdown prompts.

![Fusebox showing local MCP server controls and live output](docs/images/fusebox-demo.png)

## Download

Download the latest self-contained Windows 64-bit executable from
[GitHub Releases](https://github.com/ThomasvHoutum/Fusebox/releases/latest).
The release build does not require a separate .NET installation.

Prebuilt macOS packages are not currently published. To use Fusebox on macOS,
[build it from source](#build-from-source).

## Get started

1. Start Fusebox.
2. Select **Open configuration**. On its first run, Fusebox creates `servers.json`
   in the current user's local application-data directory.
3. Add a server definition and save the file.
4. Return to Fusebox and select **Refresh**.
5. Select the server and press **Start**.

## Configure servers and profiles

Entries live under a `servers` object and use a stable, human-readable key:

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

| Field | Required | Type | Default | Purpose |
| --- | --- | --- | --- | --- |
| Server key | Yes | String | — | Stable identifier for the entry. |
| `command` | Yes | String | — | Executable name, executable path, or complete command line. |
| `title` | No | String | Server key | Display name. |
| `description` | No | String | Empty | Display description. |
| `args` | No | Array of strings | Empty | Arguments passed directly to the executable. |
| `env` | No | Object of string values | Empty | Environment variables added to the child process. |
| `port` | No | Integer from 1 to 65535 | None | Display-only port label; Fusebox does not bind, probe, or validate it. |

Using `args` for arguments is preferred because it avoids quoting ambiguity.
Executable names registered globally can be used without a path; Fusebox inherits
the user's `PATH` in both development and published builds.

Paths containing spaces are supported. When a complete command line starts with
such a path, quote it, for example:

```json
{
  "command": "\"C:\\Program Files\\Example MCP\\server.exe\" --serve"
}
```

Commands are parsed directly and are not run through a shell, so shell operators
such as `&&`, pipes, and output redirection are not supported.

After saving the file, select **Refresh**. Changes to a running entry apply on its
next restart. Start and Restart reread the file before launching, and Restart
validates the newest definition before stopping the existing process.

Environment values are stored as plaintext. Keep the configuration file out of
source control, shared folders, and inappropriate backups.

### Server profiles

The optional top-level `profiles` object groups configured servers without moving
them to a separate file. Each profile key is its stable identifier. `servers` is
required; `title` and `description` are optional.

```json
{
  "servers": {
    "api": {
      "title": "API Server",
      "description": "Local application API",
      "command": "example-api",
      "args": ["--serve"],
      "port": 3000
    },
    "search": {
      "title": "Search Server",
      "command": "example-search",
      "port": 3001
    }
  },
  "profiles": {
    "development": {
      "title": "Development",
      "description": "Servers used for local development",
      "servers": ["api", "search"]
    }
  }
}
```

| Profile field | Required | Type | Default | Purpose |
| --- | --- | --- | --- | --- |
| Profile key | Yes | String | — | Stable profile identifier. |
| `servers` | Yes | Array of strings | — | Server IDs included in the profile. |
| `title` | No | String | Profile key | Display name on the profile card and workspace. |
| `description` | No | String | Empty | Short explanation shown with the profile controls. |

Duplicate server IDs in a profile are de-duplicated in their first-seen order, so
a server is never operated twice. Unknown IDs produce a visible warning but do not
prevent valid servers or the profile from loading. An empty profile is valid and
has no available bulk actions. Malformed profiles are reported alongside other
configuration errors while valid catalog data remains available.

Profiles and servers appear together in the catalog rail, with profiles first.
Use the rail selector to show **All**, **Servers**, or **Profiles**. Selecting a
profile opens its workspace with its title, description, aggregate runtime counts,
bulk controls, and a card for every valid referenced server. Selecting a server
card opens that server's normal detail and live-output workspace. Each profile
server card also exposes Start, Stop, and Restart without leaving the profile.

The built-in **All servers** profile represents the complete catalog. **Start all**
starts stopped or faulted servers and skips servers already running. **Stop all**
stops running servers and skips stopped servers. **Restart all** restarts running
servers and starts stopped or faulted servers. Stopping or restarting more than one
running server requires confirmation and lists every affected server.

Bulk operations run sequentially in profile order. This keeps process state,
force-quit prompts, and result summaries deterministic. A failure or canceled
force-quit affects that server only; Fusebox continues with the remaining servers
and reports the final counts. Start all and Restart all reread and validate the
configuration once before changing process state. If that read fails, no running
server is stopped. Changes to running entries continue to show as pending until
restart.

## MCP transport compatibility

Fusebox supervises local processes and captures their standard output and error. It
does not act as an MCP client or proxy, and it does not connect a process's standard
input and output to an MCP client.

Fusebox is therefore best suited to long-running MCP servers that expose a network
transport, such as Streamable HTTP or SSE. An MCP server that uses the stdio
transport should normally be launched directly by the MCP client that communicates
with it.

## Current scope

- Run one managed process per configured entry, with multiple entries running
  concurrently.
- Filter servers by profile and control the selected profile or complete catalog
  with safe bulk actions.
- Start, stop, and restart managed processes.
- Show Stopped, Starting, Running, Stopping, and Failed process states.
- Keep output in separate runs for the current application session.
- Preserve the last successfully loaded catalog when the JSON file is unreadable.
- Stop managed processes when the application closes, with confirmation and
  force-quit escalation.

Fusebox does not install MCP servers, configure MCP clients, discover external
processes, validate MCP readiness, or persist logs.

## Build from source

Building Fusebox requires the .NET 10 SDK. Windows is the primary platform; macOS
is a validation target.

```shell
dotnet restore Fusebox.sln
dotnet run --project src/Fusebox/Fusebox.csproj
```

See [Contributing](CONTRIBUTING.md) for Release builds, distribution publishing,
and the project architecture.

## Project links

- [Releases](https://github.com/ThomasvHoutum/Fusebox/releases)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Report an issue](https://github.com/ThomasvHoutum/Fusebox/issues/new)

## License

Fusebox is available under the [MIT License](LICENSE).
