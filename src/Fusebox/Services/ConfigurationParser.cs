using System.Text.Json;
using Fusebox.Models;

namespace Fusebox.Services;

public sealed class ConfigurationParser
{
    public async Task<ConfigurationLoadResult> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                stream,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip },
                cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Unreadable("The configuration root must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Object)
            {
                return Unreadable("The configuration must contain a 'servers' object.");
            }

            var entries = ParseEntries(servers);
            var profiles = ParseProfiles(document.RootElement, entries);
            return new ConfigurationLoadResult(true, entries, profiles, []);
        }
        catch (JsonException exception)
        {
            return Unreadable($"The configuration contains invalid JSON: {exception.Message}");
        }
    }

    private static IReadOnlyList<ConfigurationEntry> ParseEntries(JsonElement servers)
    {
        var properties = servers.EnumerateObject().ToList();
        var duplicateIds = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var entries = new List<ConfigurationEntry>();
        foreach (var property in properties)
        {
            var id = property.Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                entries.Add(new ConfigurationEntry("<empty key>", null, "The server key cannot be empty."));
            }
            else if (duplicateIds.Contains(id))
            {
                entries.Add(Invalid(id, $"The key '{id}' is duplicated. Every server key must be unique."));
            }
            else
            {
                entries.Add(ParseEntry(id, property.Value));
            }
        }

        return entries;
    }

    private static IReadOnlyList<ProfileEntry> ParseProfiles(
        JsonElement root,
        IReadOnlyList<ConfigurationEntry> serverEntries)
    {
        if (!root.TryGetProperty("profiles", out var profiles))
        {
            return [];
        }

        if (profiles.ValueKind != JsonValueKind.Object)
        {
            return [new ProfileEntry("profiles", null, "'profiles' must be an object.")];
        }

        var properties = profiles.EnumerateObject().ToList();
        var duplicateIds = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var knownServerIds = serverEntries
            .Where(entry => entry.IsValid)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.Ordinal);

        var entries = new List<ProfileEntry>();
        foreach (var property in properties)
        {
            var id = property.Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                entries.Add(new ProfileEntry("<empty key>", null, "The profile key cannot be empty."));
            }
            else if (duplicateIds.Contains(id))
            {
                entries.Add(new ProfileEntry(id, null, $"The profile key '{id}' is duplicated. Every profile key must be unique."));
            }
            else
            {
                entries.Add(ParseProfile(id, property.Value, knownServerIds));
            }
        }

        return entries;
    }

    private static ProfileEntry ParseProfile(string id, JsonElement element, IReadOnlySet<string> knownServerIds)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new ProfileEntry(id, null, "The profile entry must be a JSON object.");
        }

        var title = ReadOptionalString(element, "title");
        var description = ReadOptionalString(element, "description");
        if (title.Error is not null || description.Error is not null)
        {
            return new ProfileEntry(id, null, title.Error ?? description.Error);
        }

        if (!element.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
        {
            return new ProfileEntry(id, null, "'servers' is required and must be an array of server IDs.");
        }

        var values = servers.EnumerateArray().ToList();
        if (values.Any(value => value.ValueKind != JsonValueKind.String))
        {
            return new ProfileEntry(id, null, "Every value in profile 'servers' must be a string.");
        }

        var serverIds = values
            .Select(value => value.GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (serverIds.Any(string.IsNullOrWhiteSpace))
        {
            return new ProfileEntry(id, null, "Profile server IDs cannot be empty.");
        }

        var warnings = serverIds
            .Where(serverId => !knownServerIds.Contains(serverId))
            .Select(serverId => $"Unknown server reference '{serverId}'.")
            .ToList();
        return new ProfileEntry(
            id,
            new ProfileDefinition(
                id,
                title.Value ?? id,
                description.Value ?? string.Empty,
                serverIds,
                warnings),
            null);
    }

    private static ConfigurationEntry ParseEntry(string id, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid(id, "The entry must be a JSON object.");
        }

        var title = ReadOptionalString(element, "title");
        var description = ReadOptionalString(element, "description");
        var command = ReadRequiredString(element, "command");
        var arguments = ReadArguments(element);
        var environment = ReadEnvironment(element);
        var port = ReadPort(element);
        var error = new[] { title.Error, description.Error, command.Error, arguments.Error, environment.Error, port.Error }
            .FirstOrDefault(value => value is not null);
        if (error is not null)
        {
            return Invalid(id, error);
        }

        return new ConfigurationEntry(
            id,
            new ServerDefinition(
                id,
                title.Value ?? id,
                description.Value ?? string.Empty,
                command.Value!,
                arguments.Value!,
                environment.Value!,
                port.Value),
            null);
    }

    private static (string? Value, string? Error) ReadRequiredString(JsonElement element, string name)
    {
        var result = ReadOptionalString(element, name);
        return result.Error is not null || !string.IsNullOrWhiteSpace(result.Value)
            ? result
            : (null, $"'{name}' is required and cannot be empty.");
    }

    private static (string? Value, string? Error) ReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return (null, null);
        }

        return property.ValueKind == JsonValueKind.String
            ? (property.GetString(), null)
            : (null, $"'{name}' must be a string.");
    }

    private static (IReadOnlyList<string>? Value, string? Error) ReadArguments(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var property))
        {
            return ([], null);
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return (null, "'args' must be an array of strings.");
        }

        var values = property.EnumerateArray().ToList();
        return values.Any(value => value.ValueKind != JsonValueKind.String)
            ? (null, "Every value in 'args' must be a string.")
            : (values.Select(value => value.GetString()!).ToList(), null);
    }

    private static (IReadOnlyDictionary<string, string>? Value, string? Error) ReadEnvironment(JsonElement element)
    {
        if (!element.TryGetProperty("env", out var property))
        {
            return (new Dictionary<string, string>(), null);
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return (null, "'env' must be an object containing string values.");
        }

        var values = property.EnumerateObject().ToList();
        var invalid = values.FirstOrDefault(value => value.Value.ValueKind != JsonValueKind.String);
        if (invalid.Value.ValueKind != JsonValueKind.Undefined)
        {
            return (null, $"Environment value '{invalid.Name}' must be a string.");
        }

        return (values.ToDictionary(value => value.Name, value => value.Value.GetString()!, StringComparer.Ordinal), null);
    }

    private static (int? Value, string? Error) ReadPort(JsonElement element)
    {
        if (!element.TryGetProperty("port", out var property))
        {
            return (null, null);
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var port) && port is >= 1 and <= 65535
            ? (port, null)
            : (null, "'port' must be an integer from 1 through 65535.");
    }

    private static ConfigurationEntry Invalid(string id, string error) => new(id, null, error);

    private static ConfigurationLoadResult Unreadable(string error) => new(false, [], [], [error]);
}
