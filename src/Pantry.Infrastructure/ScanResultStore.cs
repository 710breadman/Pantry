using Microsoft.Data.Sqlite;
using Pantry.Domain;

namespace Pantry.Infrastructure;

public sealed class ScanResultStore
{
    private readonly PantryDatabase _database;

    public ScanResultStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task SaveAsync(
        IEnumerable<AppDetectionResult> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var result in results)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                insert into scan_results (
                    app_id,
                    scanned_utc,
                    state,
                    confidence,
                    installed_version,
                    available_version,
                    summary
                )
                values (
                    $appId,
                    $scannedUtc,
                    $state,
                    $confidence,
                    $installedVersion,
                    $availableVersion,
                    $summary
                )
                on conflict(app_id) do update set
                    scanned_utc = excluded.scanned_utc,
                    state = excluded.state,
                    confidence = excluded.confidence,
                    installed_version = excluded.installed_version,
                    available_version = excluded.available_version,
                    summary = excluded.summary;
                """;
            command.Parameters.AddWithValue("$appId", result.AppId);
            command.Parameters.AddWithValue("$scannedUtc", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$state", result.State.ToString());
            command.Parameters.AddWithValue("$confidence", result.Confidence.ToString());
            command.Parameters.AddWithValue("$installedVersion", (object?)result.InstalledVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$availableVersion", (object?)result.AvailableVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$summary", result.Summary);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, AppDetectionResult>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select app_id, state, confidence, installed_version, available_version, summary
            from scan_results;
            """;

        var results = new Dictionary<string, AppDetectionResult>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var appId = reader.GetString(0);
            results[appId] = new AppDetectionResult
            {
                AppId = appId,
                State = Enum.Parse<DetectedAppState>(reader.GetString(1)),
                Confidence = Enum.Parse<DetectionConfidence>(reader.GetString(2)),
                InstalledVersion = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvailableVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                Evidence = [],
                Summary = reader.GetString(5)
            };
        }

        return results;
    }
}

