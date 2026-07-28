namespace Fusebox.Models;

public sealed record ProfileDefinition(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> ServerIds,
    IReadOnlyList<string> Warnings);
