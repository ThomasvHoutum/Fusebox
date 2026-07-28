namespace Fusebox.Models;

public sealed record ProfileRuntime(
    string? Id,
    string Title,
    string Description,
    IReadOnlyList<string> ServerIds,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ServerRuntime> Servers)
{
    public bool IsAllServers => Id is null;

    public bool HasWarnings => Warnings.Count > 0;

    public string WarningDisplay => string.Join(Environment.NewLine, Warnings);

    public string ServerCountDisplay => Servers.Count == 1 ? "1 server" : $"{Servers.Count} servers";
}
