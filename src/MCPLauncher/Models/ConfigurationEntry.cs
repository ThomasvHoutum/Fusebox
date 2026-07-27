namespace MCPLauncher.Models;

public sealed record ConfigurationEntry(
    string Id,
    ServerDefinition? Definition,
    string? Error)
{
    public bool IsValid => Definition is not null && Error is null;
}
