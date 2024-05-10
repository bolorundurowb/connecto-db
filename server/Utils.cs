using Dapper;
using DuckDB.NET.Data;

namespace connecto.server;

public static class Utils
{
    public static Task EnsureTableCreated(DuckDBConnection connection, string tableName) => connection.ExecuteAsync(
        $"CREATE TABLE IF NOT EXISTS {tableName} (id UUID NOT NULL DEFAULT uuid(), data JSON NOT NULL)"
    );
}
