namespace Fusebox.Models;

public sealed record ConfigurationLoadResult(
    bool IsReadable,
    IReadOnlyList<ConfigurationEntry> Entries,
    IReadOnlyList<string> GlobalErrors)
{
    public bool HasErrors => GlobalErrors.Count > 0 || Entries.Any(entry => !entry.IsValid);
}
