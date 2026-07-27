using MCPLauncher.Models;

namespace MCPLauncher.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private const string ConfigurationFileName = "servers.json";
    private readonly ConfigurationParser _parser;

    public ConfigurationService(string? applicationDataDirectory = null, ConfigurationParser? parser = null)
    {
        var root = applicationDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ConfigurationDirectory = Path.Combine(root, "MCPLauncher");
        ConfigurationPath = Path.Combine(ConfigurationDirectory, ConfigurationFileName);
        _parser = parser ?? new ConfigurationParser();
    }

    public string ConfigurationDirectory { get; }

    public string ConfigurationPath { get; }

    public async Task<bool> EnsureConfigurationExistsAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ConfigurationPath))
        {
            return false;
        }

        Directory.CreateDirectory(ConfigurationDirectory);
        await File.WriteAllTextAsync(ConfigurationPath, "{\n  \"servers\": {}\n}\n", cancellationToken);
        return true;
    }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigurationPath))
        {
            return Unreadable($"Configuration file was not found at {ConfigurationPath}.");
        }

        try
        {
            await using var stream = File.OpenRead(ConfigurationPath);
            return await _parser.ParseAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unreadable($"The configuration could not be read: {exception.Message}");
        }
    }

    private static ConfigurationLoadResult Unreadable(string error) => new(false, [], [error]);
}
