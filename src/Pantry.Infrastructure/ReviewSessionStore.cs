using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Pantry.Domain;

namespace Pantry.Infrastructure;

public sealed class ReviewSessionStore
{
    public const int DefaultRetentionLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PantryDatabase _database;

    public ReviewSessionStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task<string> SaveAsync(
        DryRunPlan plan,
        string catalogVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);

        var id = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var installCount = plan.Items.Count(item => item.Intent == DryRunIntent.Install);
        var updateCount = plan.Items.Count(item => item.Intent == DryRunIntent.Update);
        var skipCount = plan.Items.Count(item => item.Intent == DryRunIntent.Skip);
        var itemsJson = JsonSerializer.Serialize(plan.Items, JsonOptions);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into review_sessions (
                id,
                created_utc,
                profile_id,
                profile_name,
                catalog_version,
                item_count,
                install_count,
                update_count,
                skip_count,
                items_json
            )
            values (
                $id,
                $createdUtc,
                $profileId,
                $profileName,
                $catalogVersion,
                $itemCount,
                $installCount,
                $updateCount,
                $skipCount,
                $itemsJson
            );
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$profileId", plan.ProfileId);
        command.Parameters.AddWithValue("$profileName", plan.ProfileName);
        command.Parameters.AddWithValue("$catalogVersion", catalogVersion);
        command.Parameters.AddWithValue("$itemCount", plan.Items.Count);
        command.Parameters.AddWithValue("$installCount", installCount);
        command.Parameters.AddWithValue("$updateCount", updateCount);
        command.Parameters.AddWithValue("$skipCount", skipCount);
        command.Parameters.AddWithValue("$itemsJson", itemsJson);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await PruneToLimitAsync(connection, DefaultRetentionLimit, cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<ReviewSessionRecord>> ListRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                id,
                created_utc,
                profile_id,
                profile_name,
                catalog_version,
                item_count,
                install_count,
                update_count,
                skip_count
            from review_sessions
            order by created_utc desc
            limit $count;
            """;
        command.Parameters.AddWithValue("$count", count);

        var records = new List<ReviewSessionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new ReviewSessionRecord
            {
                Id = reader.GetString(0),
                CreatedUtc = DateTimeOffset.Parse(reader.GetString(1)),
                ProfileId = reader.GetString(2),
                ProfileName = reader.GetString(3),
                CatalogVersion = reader.GetString(4),
                ItemCount = reader.GetInt32(5),
                InstallCount = reader.GetInt32(6),
                UpdateCount = reader.GetInt32(7),
                SkipCount = reader.GetInt32(8)
            });
        }

        return records;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from review_sessions;";

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(count);
    }

    public async Task<int> PruneToLimitAsync(
        int maxSessionsToKeep,
        CancellationToken cancellationToken = default)
    {
        if (maxSessionsToKeep < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSessionsToKeep), "Must keep at least one review session.");
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await PruneToLimitAsync(connection, maxSessionsToKeep, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> PruneToLimitAsync(
        SqliteConnection connection,
        int maxSessionsToKeep,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from review_sessions
            where id not in (
                select id
                from review_sessions
                order by created_utc desc, id desc
                limit $maxSessionsToKeep
            );
            """;
        command.Parameters.AddWithValue("$maxSessionsToKeep", maxSessionsToKeep);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
