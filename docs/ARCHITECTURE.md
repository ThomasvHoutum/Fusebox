# Architecture

Fusebox follows classic MVVM boundaries:

- `Models` contains server and profile configuration records plus observable
  runtime and session state.
- `ViewModels` owns presentation state and coordinates user workflows through
  injected abstractions.
- `Views` contains AXAML, dialogs, and the minimal window-lifecycle forwarding
  that cannot be expressed as a normal command.
- `Commands` adapts asynchronous view-model operations to `ICommand` for AXAML
  bindings.
- `Services` gives each external concern a focused implementation:
  configuration-file storage, JSON parsing, process supervision, platform file
  opening, and user interaction.

The configuration parser partially loads both server and profile entries. Profile
reference warnings do not invalidate otherwise usable definitions. The main view
model resolves profiles against the observable server catalog, projects typed
profile/server navigation items, owns catalog filtering and workspace selection,
and coordinates sequential bulk workflows through the same configuration,
interaction, and managed-process abstractions used by individual controls.
Process launch and termination behavior remains entirely in the managed process
service.

`MainWindow` is the application shell and catalog rail. `ServerWorkspaceView` and
`ProfileWorkspaceView` are presentation-only user controls bound to the main view
model. The profile server grid reuses the same individual lifecycle commands as
the server workspace, so process rules and error handling do not diverge.

`App` is the composition root. It creates the concrete services, injects them into
`MainWindowViewModel`, and assigns that view model to the window. The view does not
parse configuration, manage processes, or implement application workflows.
