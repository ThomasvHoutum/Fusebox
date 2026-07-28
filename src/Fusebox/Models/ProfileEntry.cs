namespace Fusebox.Models;

public sealed record ProfileEntry(
    string Id,
    ProfileDefinition? Definition,
    string? Error)
{
    public bool IsValid => Definition is not null && Error is null;
}
