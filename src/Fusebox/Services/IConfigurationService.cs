using Fusebox.Models;

namespace Fusebox.Services;

public interface IConfigurationService
{
    string ConfigurationPath { get; }

    Task<bool> EnsureConfigurationExistsAsync(CancellationToken cancellationToken = default);

    Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}
