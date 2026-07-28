namespace Fusebox.ViewModels;

public sealed record CatalogFilterOption(CatalogFilter Value, string Title);

public enum CatalogFilter
{
    All,
    Servers,
    Profiles
}
