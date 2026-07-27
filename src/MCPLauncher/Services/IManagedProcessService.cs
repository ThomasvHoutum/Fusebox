using MCPLauncher.Models;

namespace MCPLauncher.Services;

public interface IManagedProcessService
{
    void Start(ServerRuntime server, ServerDefinition definition);

    Task<StopResult> StopAsync(ServerRuntime server, string endReason);

    Task<bool> ForceQuitAsync(ServerRuntime server, string endReason);
}
