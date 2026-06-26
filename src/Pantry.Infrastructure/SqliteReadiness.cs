using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public static class SqliteReadiness
{
    public static string ProviderName => typeof(SqliteConnection).Namespace ?? "Microsoft.Data.Sqlite";

    public static void InitializeProvider()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
    }
}

