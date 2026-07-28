namespace MCPLauncher.Models;

public sealed record ServerDefinition(
    string Id,
    string Title,
    string Description,
    string Command,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    int? Port);
