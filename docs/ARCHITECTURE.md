# Architecture

Fusebox follows classic MVVM boundaries:

- `Models` contains configuration records and observable runtime and session state.
- `ViewModels` owns presentation state and coordinates user workflows through
  injected abstractions.
- `Views` contains AXAML, dialogs, and the minimal window-lifecycle forwarding
  that cannot be expressed as a normal command.
- `Commands` adapts asynchronous view-model operations to `ICommand` for AXAML
  bindings.
- `Services` gives each external concern a focused implementation:
  configuration-file storage, JSON parsing, process supervision, platform file
  opening, and user interaction.

`App` is the composition root. It creates the concrete services, injects them into
`MainWindowViewModel`, and assigns that view model to the window. The view does not
parse configuration, manage processes, or implement application workflows.
