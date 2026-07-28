namespace Fusebox.Models;

public sealed record ConfigurationLoadResult(
    bool IsReadable,
    IReadOnlyList<ConfigurationEntry> Entries,
    IReadOnlyList<ProfileEntry> ProfileEntries,
    IReadOnlyList<string> GlobalErrors)
{
    public bool HasErrors =>
        GlobalErrors.Count > 0 ||
        Entries.Any(entry => !entry.IsValid) ||
        ProfileEntries.Any(entry => !entry.IsValid || entry.Definition?.Warnings.Count > 0);
}
