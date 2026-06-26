using System.Text.Json;
using Microsoft.Data.Sqlite;
using Pantry.Queue;

namespace Pantry.Infrastructure;

public sealed class QueueSessionStore
{
    private readonly PantryDatabase _database;

    public QueueSessionStore(PantryDatabase database)
    {
        _database = database;
    }

    public async Task SaveAsync(
        QueueSessionPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                insert into queue_sessions (
                    id,
                    created_utc,
                    profile_id,
                    profile_name,
                    job_count,
                    review_required_count
                )
                values (
                    $id,
                    $createdUtc,
                    $profileId,
                    $profileName,
                    $jobCount,
                    $reviewRequiredCount
                );
                """;
            command.Parameters.AddWithValue("$id", plan.Id);
            command.Parameters.AddWithValue("$createdUtc", plan.CreatedUtc.ToString("O"));
            command.Parameters.AddWithValue("$profileId", plan.ProfileId);
            command.Parameters.AddWithValue("$profileName", plan.ProfileName);
            command.Parameters.AddWithValue("$jobCount", plan.Jobs.Count);
            command.Parameters.AddWithValue(
                "$reviewRequiredCount",
                plan.Jobs.Count(job => job.ReviewState == QueueJobReviewState.ReviewRequired));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var job in plan.Jobs)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                insert into queue_jobs (
                    session_id,
                    job_order,
                    app_id,
                    app_name,
                    action,
                    provider,
                    trust_level,
                    scope_preference,
                    administrator_requirement,
                    review_state,
                    review_reason,
                    dependencies_json,
                    conflicts_json
                )
                values (
                    $sessionId,
                    $jobOrder,
                    $appId,
                    $appName,
                    $action,
                    $provider,
                    $trustLevel,
                    $scopePreference,
                    $administratorRequirement,
                    $reviewState,
                    $reviewReason,
                    $dependenciesJson,
                    $conflictsJson
                );
                """;
            command.Parameters.AddWithValue("$sessionId", plan.Id);
            command.Parameters.AddWithValue("$jobOrder", job.Order);
            command.Parameters.AddWithValue("$appId", job.AppId);
            command.Parameters.AddWithValue("$appName", job.AppName);
            command.Parameters.AddWithValue("$action", job.Action.ToString());
            command.Parameters.AddWithValue("$provider", job.Provider.ToString());
            command.Parameters.AddWithValue("$trustLevel", job.TrustLevel.ToString());
            command.Parameters.AddWithValue("$scopePreference", job.ScopePreference.ToString());
            command.Parameters.AddWithValue("$administratorRequirement", job.AdministratorRequirement.ToString());
            command.Parameters.AddWithValue("$reviewState", job.ReviewState.ToString());
            command.Parameters.AddWithValue("$reviewReason", job.ReviewReason);
            command.Parameters.AddWithValue("$dependenciesJson", JsonSerializer.Serialize(job.Dependencies));
            command.Parameters.AddWithValue("$conflictsJson", JsonSerializer.Serialize(job.Conflicts));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from queue_sessions;";

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(count);
    }

    public async Task<IReadOnlyList<QueueSessionRecord>> ListRecentAsync(
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
                job_count,
                review_required_count
            from queue_sessions
            order by created_utc desc
            limit $count;
            """;
        command.Parameters.AddWithValue("$count", count);

        var records = new List<QueueSessionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new QueueSessionRecord
            {
                Id = reader.GetString(0),
                CreatedUtc = DateTimeOffset.Parse(reader.GetString(1)),
                ProfileId = reader.GetString(2),
                ProfileName = reader.GetString(3),
                JobCount = reader.GetInt32(4),
                ReviewRequiredCount = reader.GetInt32(5)
            });
        }

        return records;
    }
}
