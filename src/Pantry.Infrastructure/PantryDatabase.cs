using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public sealed class PantryDatabase
{
    private readonly string _databasePath;

    public PantryDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public SqliteConnection CreateConnection()
    {
        SqliteReadiness.InitializeProvider();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ToString();

        return new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            create table if not exists operation_logs (
                id text primary key,
                timestamp_utc text not null,
                category text not null,
                message text not null,
                details_json text null
            );

            create index if not exists ix_operation_logs_timestamp
            on operation_logs (timestamp_utc);

            create table if not exists scan_results (
                app_id text primary key,
                scanned_utc text not null,
                state text not null,
                confidence text not null,
                installed_version text null,
                available_version text null,
                summary text not null
            );

            create table if not exists app_settings (
                key text primary key,
                value text not null
            );

            create table if not exists profile_selections (
                profile_id text not null,
                app_id text not null,
                is_selected integer not null,
                updated_utc text not null,
                primary key (profile_id, app_id)
            );

            create table if not exists review_sessions (
                id text primary key,
                created_utc text not null,
                profile_id text not null,
                profile_name text not null,
                catalog_version text not null,
                item_count integer not null,
                install_count integer not null,
                update_count integer not null,
                skip_count integer not null,
                items_json text not null
            );

            create index if not exists ix_review_sessions_created
            on review_sessions (created_utc);

            create table if not exists queue_sessions (
                id text primary key,
                created_utc text not null,
                profile_id text not null,
                profile_name text not null,
                job_count integer not null,
                review_required_count integer not null
            );

            create index if not exists ix_queue_sessions_created
            on queue_sessions (created_utc);

            create table if not exists queue_jobs (
                session_id text not null,
                job_order integer not null,
                app_id text not null,
                app_name text not null,
                action text not null,
                job_status text not null default 'Planned',
                retry_mode text not null default 'ManualOnly',
                max_retry_attempts integer not null default 0,
                cancellation_behavior text not null default 'CancelBeforeStartOnly',
                failure_behavior text not null default 'PauseDependentsContinueUnrelated',
                provider text not null,
                trust_level text not null,
                scope_preference text not null,
                administrator_requirement text not null,
                review_state text not null,
                review_reason text not null,
                dependencies_json text not null,
                conflicts_json text not null,
                primary key (session_id, job_order),
                foreign key (session_id) references queue_sessions(id) on delete cascade
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(
            connection,
            "queue_jobs",
            "job_status",
            "text not null default 'Planned'",
            cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(
            connection,
            "queue_jobs",
            "retry_mode",
            "text not null default 'ManualOnly'",
            cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(
            connection,
            "queue_jobs",
            "max_retry_attempts",
            "integer not null default 0",
            cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(
            connection,
            "queue_jobs",
            "cancellation_behavior",
            "text not null default 'CancelBeforeStartOnly'",
            cancellationToken).ConfigureAwait(false);
        await EnsureColumnAsync(
            connection,
            "queue_jobs",
            "failure_behavior",
            "text not null default 'PauseDependentsContinueUnrelated'",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"pragma table_info({tableName});";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"alter table {tableName} add column {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
