using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Pantry.Infrastructure;

internal static partial class SqliteRetentionPruner
{
    public static async Task<int> PruneToLimitAsync(
        SqliteConnection connection,
        string tableName,
        string keyColumnName,
        string createdColumnName,
        int maxRowsToKeep,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        ValidateRetentionArguments(tableName, keyColumnName, createdColumnName, maxRowsToKeep);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            delete from {tableName}
            where {keyColumnName} not in (
                select {keyColumnName}
                from {tableName}
                order by {createdColumnName} desc, {keyColumnName} desc
                limit $maxRowsToKeep
            );
            """;
        command.Parameters.AddWithValue("$maxRowsToKeep", maxRowsToKeep);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task DeleteChildrenOutsideParentLimitAsync(
        SqliteConnection connection,
        string childTableName,
        string childParentKeyColumnName,
        string parentTableName,
        string parentKeyColumnName,
        string parentCreatedColumnName,
        int maxParentRowsToKeep,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        ValidateRetentionArguments(parentTableName, parentKeyColumnName, parentCreatedColumnName, maxParentRowsToKeep);
        ValidateIdentifier(childTableName);
        ValidateIdentifier(childParentKeyColumnName);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            delete from {childTableName}
            where {childParentKeyColumnName} not in (
                select {parentKeyColumnName}
                from {parentTableName}
                order by {parentCreatedColumnName} desc, {parentKeyColumnName} desc
                limit $maxParentRowsToKeep
            );
            """;
        command.Parameters.AddWithValue("$maxParentRowsToKeep", maxParentRowsToKeep);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateRetentionArguments(
        string tableName,
        string keyColumnName,
        string createdColumnName,
        int maxRowsToKeep)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(keyColumnName);
        ValidateIdentifier(createdColumnName);

        if (maxRowsToKeep < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRowsToKeep), "Must keep at least one row.");
        }
    }

    private static void ValidateIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (!SqlIdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException($"Invalid SQLite identifier: {identifier}", nameof(identifier));
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SqlIdentifierPattern();
}
