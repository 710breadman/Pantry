using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public sealed class AppSelectionStore
{
    private readonly PantryDatabase _database;

    public AppSelectionStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyDictionary<string, bool>> LoadAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select app_id, is_selected
            from profile_selections
            where profile_id = $profileId;
            """;
        command.Parameters.AddWithValue("$profileId", profileId);

        var selections = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            selections[reader.GetString(0)] = reader.GetInt32(1) == 1;
        }

        return selections;
    }

    public async Task SaveAsync(
        string profileId,
        string appId,
        bool isSelected,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into profile_selections (profile_id, app_id, is_selected, updated_utc)
            values ($profileId, $appId, $isSelected, $updatedUtc)
            on conflict(profile_id, app_id) do update set
                is_selected = excluded.is_selected,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$profileId", profileId);
        command.Parameters.AddWithValue("$appId", appId);
        command.Parameters.AddWithValue("$isSelected", isSelected ? 1 : 0);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

