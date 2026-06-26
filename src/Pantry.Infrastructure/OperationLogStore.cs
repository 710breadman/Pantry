using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public sealed class OperationLogStore
{
    private readonly PantryDatabase _database;

    public OperationLogStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task AppendAsync(
        string category,
        string message,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into operation_logs (id, timestamp_utc, category, message, details_json)
            values ($id, $timestampUtc, $category, $message, $detailsJson);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$timestampUtc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$detailsJson", (object?)detailsJson ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OperationLogEntry>> ListRecentAsync(
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
            select id, timestamp_utc, category, message, details_json
            from operation_logs
            order by timestamp_utc desc
            limit $count;
            """;
        command.Parameters.AddWithValue("$count", count);

        var entries = new List<OperationLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new OperationLogEntry
            {
                Id = reader.GetString(0),
                TimestampUtc = DateTimeOffset.Parse(reader.GetString(1)),
                Category = reader.GetString(2),
                Message = reader.GetString(3),
                DetailsJson = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return entries;
    }
}

