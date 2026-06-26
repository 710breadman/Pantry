using System.Text.Json;
using Microsoft.Data.Sqlite;
using Pantry.Domain;
using Pantry.Queue;

namespace Pantry.Infrastructure;

public sealed class QueueSessionStore
{
    public const int DefaultRetentionLimit = 100;

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
                    job_status,
                    retry_mode,
                    max_retry_attempts,
                    cancellation_behavior,
                    failure_behavior,
                    provider,
                    trust_level,
                    scope_preference,
                    administrator_requirement,
                    review_state,
                    review_reason,
                    dependencies_json,
                    blocked_by_app_ids_json,
                    conflicts_json
                )
                values (
                    $sessionId,
                    $jobOrder,
                    $appId,
                    $appName,
                    $action,
                    $jobStatus,
                    $retryMode,
                    $maxRetryAttempts,
                    $cancellationBehavior,
                    $failureBehavior,
                    $provider,
                    $trustLevel,
                    $scopePreference,
                    $administratorRequirement,
                    $reviewState,
                    $reviewReason,
                    $dependenciesJson,
                    $blockedByAppIdsJson,
                    $conflictsJson
                );
                """;
            command.Parameters.AddWithValue("$sessionId", plan.Id);
            command.Parameters.AddWithValue("$jobOrder", job.Order);
            command.Parameters.AddWithValue("$appId", job.AppId);
            command.Parameters.AddWithValue("$appName", job.AppName);
            command.Parameters.AddWithValue("$action", job.Action.ToString());
            command.Parameters.AddWithValue("$jobStatus", job.Status.ToString());
            command.Parameters.AddWithValue("$retryMode", job.RetryMode.ToString());
            command.Parameters.AddWithValue("$maxRetryAttempts", job.MaxRetryAttempts);
            command.Parameters.AddWithValue("$cancellationBehavior", job.CancellationBehavior.ToString());
            command.Parameters.AddWithValue("$failureBehavior", job.FailureBehavior.ToString());
            command.Parameters.AddWithValue("$provider", job.Provider.ToString());
            command.Parameters.AddWithValue("$trustLevel", job.TrustLevel.ToString());
            command.Parameters.AddWithValue("$scopePreference", job.ScopePreference.ToString());
            command.Parameters.AddWithValue("$administratorRequirement", job.AdministratorRequirement.ToString());
            command.Parameters.AddWithValue("$reviewState", job.ReviewState.ToString());
            command.Parameters.AddWithValue("$reviewReason", job.ReviewReason);
            command.Parameters.AddWithValue("$dependenciesJson", JsonSerializer.Serialize(job.Dependencies));
            command.Parameters.AddWithValue("$blockedByAppIdsJson", JsonSerializer.Serialize(job.BlockedByAppIds));
            command.Parameters.AddWithValue("$conflictsJson", JsonSerializer.Serialize(job.Conflicts));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await PruneToLimitAsync(DefaultRetentionLimit, cancellationToken).ConfigureAwait(false);
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

    public async Task<int> CountJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from queue_jobs;";

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

    public async Task<IReadOnlyList<QueueJobRecord>> ListJobsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                job_order,
                app_id,
                app_name,
                action,
                job_status,
                retry_mode,
                max_retry_attempts,
                cancellation_behavior,
                failure_behavior,
                provider,
                trust_level,
                review_state,
                review_reason,
                blocked_by_app_ids_json
            from queue_jobs
            where session_id = $sessionId
            order by job_order;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        var records = new List<QueueJobRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            records.Add(new QueueJobRecord
            {
                Order = reader.GetInt32(0),
                AppId = reader.GetString(1),
                AppName = reader.GetString(2),
                Action = Enum.Parse<QueueJobAction>(reader.GetString(3)),
                Status = Enum.Parse<QueueJobStatus>(reader.GetString(4)),
                RetryMode = Enum.Parse<QueueRetryMode>(reader.GetString(5)),
                MaxRetryAttempts = reader.GetInt32(6),
                CancellationBehavior = Enum.Parse<QueueCancellationBehavior>(reader.GetString(7)),
                FailureBehavior = Enum.Parse<QueueFailureBehavior>(reader.GetString(8)),
                Provider = Enum.Parse<ProviderType>(reader.GetString(9)),
                TrustLevel = Enum.Parse<TrustLevel>(reader.GetString(10)),
                ReviewState = Enum.Parse<QueueJobReviewState>(reader.GetString(11)),
                ReviewReason = reader.GetString(12),
                BlockedByAppIds = JsonSerializer.Deserialize<string[]>(reader.GetString(13)) ?? []
            });
        }

        return records;
    }

    public async Task<int> PruneToLimitAsync(
        int maxSessionsToKeep,
        CancellationToken cancellationToken = default)
    {
        if (maxSessionsToKeep < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSessionsToKeep), "Must keep at least one queue session.");
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var sqliteTransaction = (SqliteTransaction)transaction;
        await SqliteRetentionPruner.DeleteChildrenOutsideParentLimitAsync(
                connection,
                "queue_jobs",
                "session_id",
                "queue_sessions",
                "id",
                "created_utc",
                maxSessionsToKeep,
                cancellationToken,
                sqliteTransaction)
            .ConfigureAwait(false);
        var deletedSessions = await SqliteRetentionPruner.PruneToLimitAsync(
                connection,
                "queue_sessions",
                "id",
                "created_utc",
                maxSessionsToKeep,
                cancellationToken,
                sqliteTransaction)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deletedSessions;
    }
}
