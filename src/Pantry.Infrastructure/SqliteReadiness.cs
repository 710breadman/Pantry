using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

public static class SqliteReadiness
{
    public static string ProviderName => typeof(SqliteConnection).Namespace ?? "Microsoft.Data.Sqlite";
}

