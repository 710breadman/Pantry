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
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
