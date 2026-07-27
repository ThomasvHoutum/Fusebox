# MCP Launcher Product Plan

## Document status

This document defines the product behavior and scope that should guide later technical planning. It intentionally does not select an architecture, libraries, process APIs, packaging approach, or implementation sequence.

The initial release is a personal, Windows-first desktop utility. macOS must be used to validate the same core product workflows, but it is not an equal first-release optimization target.

## Product summary

MCP Launcher is a desktop utility for developers who manually run self-hosted MCP servers. It stores reusable launch definitions in its own JSON configuration and provides one recognizable place to start, monitor, inspect, restart, and stop those processes without recalling commands or managing separate terminal windows.

Although the intended entries are MCP servers, the launcher does not need protocol-aware behavior. From the application's perspective, an entry is a configured long-running process. The product remains focused through its terminology, defaults, documentation, and supported workflows rather than through MCP protocol validation.

### Core promise

> Configure an MCP launch command once, then start it and find its current output in seconds.

## Problem statement

The current workflow distributes basic MCP process management across shell history, personal notes, documentation pages, and multiple terminal windows:

1. Begin a development session in a tool such as Codex, Cursor, or Unity.
2. Open a terminal and search its history for an MCP startup command.
3. If the command cannot be found, search personal notes.
4. If it is not recorded there, find the MCP documentation again.
5. Start the server in a terminal that remains occupied by that process.
6. Open another terminal and repeat the workflow for a second MCP.
7. Later, inspect multiple terminal windows to locate the MCP that produced a relevant log or error.

This creates four recurring costs:

- Startup commands must be rediscovered or remembered.
- Each MCP occupies a separate terminal window.
- Terminal windows do not provide a stable, descriptive server overview.
- Logs and launch failures are harder to locate than they should be.

The typical scale is only one or two concurrently running MCP servers, and lifecycle actions are relatively infrequent. Therefore, the product should prioritize dependable recall, clear identity, lifecycle visibility, and centralized logs rather than sophisticated orchestration.

## Target users

### Primary user

A developer who:

- Uses AI development tools such as Codex and Cursor, potentially alongside Unity.
- Runs self-hostable, third-party MCP servers rather than necessarily developing those servers.
- Manually launches one or two MCP servers for a development session.
- Wants the servers to remain active until explicitly stopped or until MCP Launcher closes.
- Does not want to recover commands or identify servers through anonymous terminal windows.

Headroom and Unity MCP are representative examples for validation.

### Initial adoption model

The first version is a personal utility. Team workflows, broad configuration compatibility, shared catalogs, and general open-source adoption are not initial design drivers.

### Operating environment

- Windows is the primary environment.
- The core workflows must also be validated on macOS.
- Definitions are generally shared across development sessions rather than organized by project.
- MCP Launcher manages only processes that it starts.

## Core value proposition

MCP Launcher replaces a fragmented process-management routine with a persistent catalog of recognizable MCP servers. A user can define a startup command once, launch the server from a named row, see whether its process is alive, inspect all output from the current application session, and stop or restart it without finding the correct terminal window.

The application reports process lifecycle state. It does not claim that a running process is a healthy, ready, reachable, or correctly connected MCP server.

## Product boundaries

### MCP Launcher is

- A persistent catalog of named MCP launch definitions.
- A manual launcher and lifecycle manager for processes it starts.
- A consolidated, current-session process-output viewer.
- A display for simple configured metadata such as a port.
- A personal desktop utility with explicit user control.

### MCP Launcher is not

- An MCP client, gateway, proxy, or protocol debugger.
- A health-monitoring or readiness-validation system.
- A manager for Codex or Cursor configuration.
- A process-discovery or process-adoption tool.
- A terminal emulator.
- A general process manager, system service manager, or Docker replacement.
- An installation or package-management system.
- A remote server or project orchestrator.

## Primary use cases

1. Register a reusable MCP launch definition in JSON.
2. Refresh the catalog after editing configuration.
3. Start one configured server from its row.
4. Inspect whether a managed process is stopped, starting, running, stopping, or failed.
5. Open a server detail view and inspect the current run's output.
6. Diagnose a command that could not be launched or a process that exited unexpectedly.
7. Stop a running server.
8. Restart a server using the most recently saved valid definition.
9. View the server's configured port.
10. Close the launcher and intentionally terminate all of its managed processes.

## User workflows

### First launch

**Goal:** Understand how to register the first server without treating normal setup as an error.

1. If no configuration has ever existed, MCP Launcher creates an empty configuration file.
2. The application presents an empty state explaining that no servers are registered.
3. A prominent **Open configuration file** action opens the file in the operating system's associated editor.
4. The empty state explains the edit, save, return, and **Refresh** workflow.
5. The application does not insert example rows that could be mistaken for runnable servers.

If a configuration file existed previously and is later deleted, that is a configuration failure rather than a new first launch. The last successfully loaded catalog remains visible.

### Add a server

**Goal:** Store a command once so it does not need to be recovered again.

1. Select **Open configuration file**.
2. Add an entry under a unique, human-readable key such as `unity-mcp`.
3. Supply its title, description, command, arguments, environment values, and optional port.
4. Save the file.
5. Return to MCP Launcher and select **Refresh**.
6. A valid entry appears as a stopped row.

An invalid but identifiable entry appears as a disabled row with a specific configuration error. An error that cannot be associated with one entry is available through the global configuration warning.

### Edit a stopped server

**Goal:** Change the next launch definition.

1. Open, edit, and save the JSON file.
2. Select **Refresh**.
3. The row reflects the updated saved definition.
4. The next Start rereads and uses the latest valid entry from disk.

### Edit a running server

**Goal:** Change a future run without disrupting the current process.

1. Edit and save the entry.
2. Select **Refresh**.
3. The current process remains unchanged.
4. The row indicates that saved changes apply after restart.
5. The next Restart rereads the file again, validates the newest entry, and applies it only if valid.

### Start a server

**Goal:** Launch a known MCP without reconstructing its command.

1. Select **Start** on the server row.
2. MCP Launcher rereads and validates that entry from JSON.
3. State changes from **Stopped** to **Starting**.
4. A distinct run record is created for the attempt.
5. Successful process creation immediately changes the state to **Running**.
6. Output is available in the server detail view from the beginning of the attempt.

If the definition is invalid, no launch is attempted. If the process cannot be created, the state becomes **Failed** and the failed run retains available diagnostic information.

### Inspect status and logs

**Goal:** Locate the correct server and its output without searching terminal windows.

1. Review lifecycle state in the overview.
2. Select a server row.
3. View and scroll through the selected run's output.
4. Move between separate runs created by starts and restarts during the current application session.

Each run identifies its start time, end time when applicable, lifecycle events, and end reason. End reasons distinguish user stop, user restart, launch failure, unexpected exit, and application shutdown.

### Diagnose a failed launch or unexpected exit

**Goal:** Understand why the expected process is not running.

1. The affected row becomes visually prominent in the **Failed** state.
2. MCP Launcher does not unexpectedly navigate away from the user's current view and does not send an operating-system notification.
3. The user selects the row to inspect its failed run.
4. The run presents launcher-provided failure information and any captured process output.
5. Failed remains visible until the user explicitly starts the server again.

### Stop a server

**Goal:** End a managed process without finding its terminal or operating-system process.

1. Select **Stop** without an additional confirmation.
2. State changes from **Running** to **Stopping**.
3. MCP Launcher requests termination.
4. Successful termination changes the state to **Stopped** and completes the run.
5. If the process resists termination, offer **Force quit** and **Cancel**.
6. **Force quit** forcibly terminates the process.
7. **Cancel** abandons the stop attempt and returns the entry to **Running**.

### Restart a server

**Goal:** Replace a run using the newest saved definition without avoidable downtime.

1. Select **Restart**.
2. MCP Launcher rereads and validates the entry from JSON before stopping anything.
3. If invalid, the existing process remains running and the configuration error is shown.
4. If valid, MCP Launcher uses the normal stop sequence.
5. If the user cancels a resisted termination, no replacement run starts.
6. After successful termination, the previous run completes and a distinct new run begins.
7. The new run uses the entry that was read and validated at the start of Restart.

### Refresh configuration

**Goal:** Reconcile the displayed catalog with an externally edited file.

- A new valid entry produces a stopped row.
- A new identifiable invalid entry produces a disabled row with a specific error.
- A changed stopped entry updates immediately.
- A changed running entry updates its saved definition, leaves the process unchanged, and indicates that changes apply after restart.
- A removed stopped entry disappears.
- A removed running entry triggers a confirmation naming every affected server.

If the user cancels removal confirmation, the catalog and processes remain unchanged. If termination fails, the affected row remains visible so control is not lost.

### Handle an unreadable configuration file

**Goal:** Recover safely from a partially edited, malformed, missing, or otherwise unreadable file.

- Keep the last successfully loaded catalog visible.
- Mark the overall configuration as invalid and explain that the catalog is stale.
- Do not add, update, or remove rows during the failed refresh.
- Do not stop existing processes.
- Keep Stop available.
- Block Start and Restart because the latest definitions cannot be read safely.
- Let the user open and correct the file, then explicitly Refresh again.

### Close and reopen MCP Launcher

**Goal:** Exit without unknowingly abandoning or terminating managed processes.

1. If no process is running, close normally.
2. If processes are running, warn that closing terminates them and name each one.
3. Cancel leaves the launcher and its processes running.
4. Confirm uses the normal termination behavior.
5. If any process resists, offer **Force quit** and **Cancel**.
6. **Force quit** forcibly terminates the remaining processes before the application closes.
7. **Cancel** abandons shutdown and leaves MCP Launcher open.
8. When reopened, every configured entry starts as **Stopped** and prior run logs are not restored.

MCP Launcher neither discovers nor adopts processes that are already running outside it.

## Functional requirements

### Configuration

- **FR-C01:** MCP Launcher must own an independent JSON configuration file.
- **FR-C02:** It must not import, synchronize, or modify Codex or Cursor configuration.
- **FR-C03:** Each entry must use a unique, user-chosen, human-readable string key as its stable identity.
- **FR-C04:** A display title may change without changing stable identity.
- **FR-C05:** A changed key must be treated as removal of the old entry and addition of a new entry.
- **FR-C06:** Duplicate keys must invalidate all entries sharing that key while leaving uniquely keyed entries usable.
- **FR-C07:** Entries must support title, description, startup command, arguments, and environment values.
- **FR-C08:** Entries may include an optional, explicitly configured port.
- **FR-C09:** The application must provide **Open configuration file** and **Refresh** actions.
- **FR-C10:** The application must not require automatic file watching.
- **FR-C11:** Valid entries must remain usable when other individually identifiable entries are invalid.
- **FR-C12:** The overview must expose a global warning when one or more configuration errors exist.
- **FR-C13:** An identifiable invalid entry must remain visible but disabled and expose its specific error.
- **FR-C14:** Whole-file or unidentifiable errors must be exposed through the global warning.
- **FR-C15:** An unreadable file must preserve the last successfully loaded catalog and block Start and Restart without blocking Stop.
- **FR-C16:** Start and Restart must reread the relevant saved entry rather than assume the last refreshed definition is current.

### Overview and lifecycle

- **FR-L01:** The overview must present one row per identifiable configured entry.
- **FR-L02:** Each row must present title, description, lifecycle state, and configured port when present.
- **FR-L03:** Each valid row must expose the lifecycle actions appropriate to its current state.
- **FR-L04:** Servers must be started individually; bulk lifecycle actions are not required.
- **FR-L05:** The lifecycle model must include Stopped, Starting, Running, Stopping, and Failed.
- **FR-L06:** Configuration error must be presented independently of lifecycle state.
- **FR-L07:** Running must mean only that process creation succeeded and the process has not exited.
- **FR-L08:** Successful process creation must immediately transition Starting to Running without a readiness delay.
- **FR-L09:** An unexpected exit must transition Running to Failed.
- **FR-L10:** Failed must persist until the next explicit lifecycle action.
- **FR-L11:** The launcher must not automatically start or restart a server.
- **FR-L12:** The launcher must manage only processes that it started.
- **FR-L13:** A running entry with a refreshed definition must indicate that changes apply after restart.
- **FR-L14:** Removing running entries during Refresh must require confirmation that names each affected entry.
- **FR-L15:** A row must remain visible until its managed process is confirmed to have ended.

### Stop, restart, and shutdown

- **FR-S01:** Stop must request termination and expose Stopping until the result is known.
- **FR-S02:** A resisted termination must offer Force quit and Cancel.
- **FR-S03:** Canceling a stop must leave the process represented as Running.
- **FR-S04:** Restart must validate the newest saved definition before stopping the existing process.
- **FR-S05:** Invalid updated configuration must not terminate an existing running process.
- **FR-S06:** Restart must not launch a replacement when termination is canceled or unsuccessful.
- **FR-S07:** Closing with running processes must warn the user and name each affected entry.
- **FR-S08:** Closing MCP Launcher must terminate its managed processes after confirmation.
- **FR-S09:** Resisted termination during shutdown must offer Force quit and Cancel.
- **FR-S10:** Canceling shutdown must leave MCP Launcher open.

### Logs and details

- **FR-D01:** Selecting a server row must open its detail view.
- **FR-D02:** The detail view must show process output captured during the current application session.
- **FR-D03:** The log experience must support viewing and scrolling.
- **FR-D04:** Each start or restart must create a distinct run rather than append indistinguishably to the previous run.
- **FR-D05:** Runs must show lifecycle events and an end reason when applicable.
- **FR-D06:** Launch failures and unexpected exits must retain available diagnostics for the current session.
- **FR-D07:** Current-session runs must not be restored after application exit.
- **FR-D08:** The first version does not need search, filtering, export, severity classification, or interactive input.
- **FR-D09:** The first version must not present a user-visible log-truncation policy.

## Non-functional product expectations

These describe user-visible quality rather than implementation choices.

### Predictability

- Refresh must never terminate a process as an unannounced side effect.
- Restart must not stop a working process before validating its replacement definition.
- The product must not label a process healthy merely because it is alive.
- A process that remains alive must never disappear from the catalog solely because its configuration entry changed or was removed.

### Recoverability

- Temporary JSON mistakes must not erase the last usable catalog.
- Configuration failures must identify the affected entry when possible.
- Failed launches and unexpected exits must remain inspectable for the current session.
- Destructive reconciliation and application shutdown must be cancelable.

### Clarity

- Lifecycle state and configuration validity must be distinguishable.
- Running must be described as process state, not readiness.
- Separate runs and end reasons must prevent old output from being mistaken for a new execution.
- Force quit must be visually distinct from normal termination.

### Platform experience

- The primary Windows experience must support the complete first-version workflow.
- The same core user journey and configuration concepts must be validated on macOS.
- Platform differences must not change the meaning of lifecycle states or safety confirmations.

### Local data expectations

- Environment values may be stored as plaintext in the local JSON file.
- The product need not provide a secret store or masking interface in the first version.
- Sensitive environment values must not be unnecessarily repeated in the overview or launcher-generated lifecycle messages.
- Users must be informed that they are responsible for protecting the configuration file from inappropriate sharing, synchronization, backups, or source control.

### Session output

- Users should experience complete output for all runs in the current application session.
- There is no user-visible truncation behavior in the first version.
- Safe handling of pathological output volume is a technical-planning question and must not silently contradict the product expectation.

## First-version scope

The first useful version includes:

- Launcher-owned, externally editable JSON configuration.
- Human-readable stable entry keys.
- Title, description, command, arguments, environment values, and optional port.
- Open-file and manual Refresh actions.
- Partial configuration loading and actionable error presentation.
- Named server overview with per-row lifecycle controls.
- Stopped, Starting, Running, Stopping, and Failed states.
- Safe Start, Stop, Restart, Refresh, and shutdown behavior.
- Current-session process output divided into separate runs.
- Viewing and scrolling through logs.
- Windows-first behavior with macOS workflow validation.

## Later opportunities

### Valuable shortly after launch

- One configured dashboard URL per entry.
- Opening a dashboard in the system's default browser.
- Copying the configured port.
- Exporting a run or server's current-session logs.
- Clearing current-session logs.
- Better onboarding, example configuration, and error-location assistance.

### Experimental or uncertain

- Multiple dashboard URLs.
- Configurable health or readiness endpoints.
- Automatic warning and error classification.
- Unviewed-warning or error indicators.
- Start all, stop all, or remembered server sets.
- Project groupings.
- Configurable graceful-shutdown commands.
- Background or system-tray operation.
- Limited interactive process input.
- Automatic endpoint discovery.

These opportunities should be promoted only when real usage demonstrates that they solve a recurring problem.

## Explicit non-goals

- Installing or uninstalling MCP servers.
- Delete actions that invoke arbitrary uninstall commands.
- Managing, importing, or synchronizing Codex or Cursor configuration.
- Configuring how clients connect to a launched server.
- Discovering or adopting externally started processes.
- Auto-start and automatic restart policies.
- MCP protocol validation, traffic inspection, gateway, or proxy behavior.
- Health or readiness claims in the first version.
- Full terminal emulation.
- General Docker, service-orchestration, or remote-management behavior.
- Built-in graphical JSON editing.
- Project management, grouping, and filtering.
- Persistent log history.
- First-version log search, filtering, classification, or export.
- First-version dashboard actions.
- Dedicated application-managed secret storage.

## Risks and unresolved questions

### Product risks

1. **Low usage frequency:** Development sessions are not daily and only one or two servers run. The friction may be real but too infrequent to sustain a dedicated application.
2. **Generic-process ambiguity:** The product is MCP-focused in purpose but manages generic long-running commands. It could drift into a general process manager without disciplined scope control.
3. **Alive is not healthy:** Some MCPs may remain alive while unusable, making Running insufficient in practice.
4. **JSON authoring friction:** External editing may be less convenient than expected, especially for commands that differ across operating systems.
5. **Viewing-only logs:** Real servers may require search, interactive input, or terminal-specific formatting for diagnosis.
6. **Shutdown expectations:** Users may want to close the launcher while leaving servers running, which would challenge the foreground lifecycle model.
7. **Plaintext secrets:** Local JSON may be copied to cloud storage, backups, support messages, or source control.
8. **Unbounded session output:** Very verbose processes may create usability or resource problems even though visible truncation is not desired.
9. **macOS parity:** Representative commands and process behavior may expose product-level differences not represented by the current entry model.

### Unresolved product questions to validate

- Do Headroom and Unity MCP require important entry information beyond the currently agreed fields, such as a working location?
- Do representative command definitions need visibly different values on Windows and macOS?
- Is a port known in advance for the representative servers, and how useful is it in the overview?
- Do representative servers ever remain alive while unusable?
- Are viewing and scrolling enough for normal diagnosis?
- Does MCP Launcher naturally remain open for the full development session?
- Are the proposed workflow success thresholds meaningful after baseline measurement?

## Validation plan

| Assumption | Why it matters | Cheapest useful method | Supporting result | Direction-changing result |
| --- | --- | --- | --- | --- |
| Command retrieval creates meaningful friction. | A dedicated launcher must save more effort than it introduces. | Record command source, retrieval time, mistakes, and terminal count for the next three development sessions. | Retrieval is repeatedly noticeable, error-prone, or disruptive. | Commands are consistently launched almost immediately; log organization must provide most of the value. |
| External JSON editing is acceptable. | Avoiding an editor keeps the first version focused. | Describe Headroom and Unity MCP in the proposed format; add, edit, break, and correct entries. | Both are easy to express and correct. | Authoring is confusing or platform differences make entries awkward; stronger guidance or an editor becomes necessary. |
| Process-alive state is sufficient. | Health checks would add substantial MCP-specific scope. | Observe whether representative servers remain alive while failing initialization or becoming unusable. | Meaningful failures prevent launch or cause exit. | Alive-but-unusable behavior is common; explicit readiness becomes essential. |
| Centralized viewing-only logs replace terminal windows. | Log consolidation is core value. | Diagnose representative normal runs and failures using only viewing, scrolling, separate runs, and lifecycle messages. | Typical diagnosis requires no terminal input, search, or special formatting. | Interactive input, search, or terminal behavior is frequently necessary. |
| Individual controls suit the normal scale. | Bulk controls and grouping would otherwise be missing. | Track configured and concurrent servers across real sessions. | One or two servers remain normal and individual starts feel effortless. | Several servers are routinely launched as a set. |
| Closing should terminate servers. | The foreground lifecycle avoids orphaned processes but may conflict with user expectations. | Observe whether the launcher naturally remains open through development and whether close confirmation is sufficient. | Closing corresponds to ending the MCP session. | The launcher is often closed while servers must remain active; background behavior becomes important. |
| One product model works on Windows and macOS. | macOS validation is an explicit expectation. | Compare representative definitions and lifecycle outcomes on both systems. | The same visible fields and workflows describe both. | Platform-specific behavior requires different entries or a Windows-only initial release. |
| Plaintext values are acceptable for personal use. | API keys can be sensitive despite local-only operation. | Review whether the config location is shared, synchronized, backed up, source-controlled, or accessible by others. | It can remain private and the user accepts plaintext responsibility. | Sharing or synchronization is necessary; references or secret handling require reconsideration. |

## Success criteria

### Workflow success

For a previously configured server:

- The user can find and start it in approximately 15 seconds.
- No dedicated terminal window is needed.
- The correct server's current output can be reached in approximately 10 seconds.
- A failed launch can be located and understood from MCP Launcher in approximately 30 seconds.

These are validation targets rather than implementation performance guarantees. They should be compared with measured baseline sessions.

### Behavioral success

- The user no longer searches shell history, notes, or documentation for a previously registered startup command.
- The user can identify each running MCP without inspecting terminal contents.
- Intentional stop and unexpected exit are never represented as the same outcome.
- Invalid configuration cannot unnecessarily terminate an existing running server.
- Temporary whole-file errors do not erase the last usable catalog.
- Closing the launcher never silently abandons or terminates managed processes.

### Scope success

- The first useful version solves the two central problems: reusable launch commands and centralized server output.
- It remains useful without health checks, persistent logs, interactive terminal input, dashboard links, bulk orchestration, or installation management.
- Later opportunities are driven by observed usage rather than resemblance to Docker Desktop.

## Decisions required before technical planning

The following product inputs should be settled or validated before converting this plan into a technical design:

1. Produce redacted representative configurations for Headroom and Unity MCP on Windows.
2. Determine whether the agreed entry fields can also describe the macOS launch workflow without confusing conditional behavior.
3. Decide whether a working-location field is a required product concept based on the representative commands.
4. Confirm whether server ports are normally known in advance and useful enough to retain as essential overview metadata.
5. Observe whether process-alive state is sufficient for the representative MCPs.
6. Validate that viewing and scrolling cover actual diagnostic needs.
7. Measure at least three baseline development sessions against the proposed success thresholds.
8. Confirm that closing MCP Launcher normally corresponds to ending the MCP session.
9. Choose and document an appropriate private location and user warning for plaintext configuration values.
10. Resolve how product expectations for complete current-session output coexist with safe behavior under pathological output volume, without exposing unexpected silent truncation.

Only after these product inputs are understood should technical planning address architecture, subprocess control, cross-platform command representation, output capture, JSON schema details, persistence mechanisms, packaging, signing, distribution, implementation milestones, or library selection.

## Decision record

### Confirmed

- MCP Launcher is initially a personal utility for developers manually running self-hosted MCP servers.
- Windows is primary; macOS receives core-workflow validation.
- The launcher owns an independent JSON file and does not interact with Codex or Cursor configuration.
- The application manages only processes it starts.
- Entries use stable human-readable keys.
- Configuration is edited externally and explicitly refreshed.
- Servers are started individually.
- Running means process alive, not MCP health or readiness.
- Lifecycle states are Stopped, Starting, Running, Stopping, and Failed.
- Invalid entries do not prevent uniquely identifiable valid entries from being used.
- Start and Restart reread the latest JSON; Restart validates before stopping a running process.
- Removed running entries require a named confirmation before Refresh stops them.
- Logs cover the current application session, use separate runs, and support viewing and scrolling.
- Closing the application terminates managed processes after confirmation.
- Resisted termination offers Force quit and Cancel.
- Dashboard links, copy-port, and log export are post-launch opportunities.
- Installation and uninstallation are non-goals.
- Special secret treatment, grouping, filtering, auto-start, automatic restart, and operating-system notifications are not required.

### Working assumptions

- Command recall and centralized logs jointly justify a dedicated utility.
- External JSON editing remains acceptable in real use.
- Headroom and Unity MCP fit the agreed entry model.
- The usual scale remains one or two concurrent servers.
- Process-alive state provides enough feedback for a first version.
- Viewing-only logs replace the relevant terminal workflow.
- The launcher remains open throughout development.
- Plaintext environment values are acceptable when the file is responsibly protected.

### Open questions

- Results of the validation plan.
- Whether a working location belongs in the product definition.
- Whether representative definitions require platform-specific product behavior.
- Whether alive-but-unusable servers make readiness checks necessary.
- Whether the baseline supports the proposed time-based success targets.
- Whether complete current-session output presents a user-visible edge case that needs a product decision.

### Deferred

- Application architecture and component boundaries.
- Process and process-tree management mechanisms.
- JSON schema syntax, versioning, and migration.
- Library and framework choices beyond the already established Avalonia product context.
- Log storage and resource-management implementation.
- Secure-storage implementation options.
- Packaging, signing, distribution, and update strategy.
- Engineering milestones, estimates, and implementation plan.
