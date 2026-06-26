using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public sealed class UserSettingsStore
{
    private const string SelectedProfileIdKey = "selected_profile_id";
    private const string PortableDestinationKey = "portable_destination";
    private readonly PantryDatabase _database;

    public UserSettingsStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var values = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return new UserSettings
        {
            SelectedProfileId = values.GetValueOrDefault(SelectedProfileIdKey),
            PortableDestination = values.GetValueOrDefault(PortableDestinationKey)
        };
    }

    public Task SaveSelectedProfileIdAsync(string profileId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        return SaveAsync(SelectedProfileIdKey, profileId, cancellationToken);
    }

    public Task SavePortableDestinationAsync(string portableDestination, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portableDestination);
        return SaveAsync(PortableDestinationKey, portableDestination, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "select key, value from app_settings;";

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    private async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into app_settings (key, value)
            values ($key, $value)
            on conflict(key) do update set value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

