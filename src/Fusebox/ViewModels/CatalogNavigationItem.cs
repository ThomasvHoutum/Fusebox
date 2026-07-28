using Fusebox.Models;

namespace Fusebox.ViewModels;

public sealed class CatalogNavigationItem
{
    public CatalogNavigationItem(ProfileRuntime profile)
    {
        Profile = profile;
        Key = $"profile:{profile.Id ?? "<all>"}";
    }

    public CatalogNavigationItem(ServerRuntime server)
    {
        Server = server;
        Key = $"server:{server.Id}";
    }

    public string Key { get; }

    public ProfileRuntime? Profile { get; }

    public ServerRuntime? Server { get; }

    public bool IsProfile => Profile is not null;

    public bool IsServer => Server is not null;
}
