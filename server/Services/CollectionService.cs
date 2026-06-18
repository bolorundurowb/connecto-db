using Dapper;
using DuckDB.NET.Data;

namespace ConnectoDb.Server.Services;

public class CollectionService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;

    public CollectionService(string dbName)
    {
        _dbConnection = new DuckDBConnection($"Data Source={dbName}");
        _dbConnection.Open();
    }

    public async Task<List<string>> GetAll()
    {
        var tables = await _dbConnection.QueryAsync<string>("SHOW TABLES");
        return tables
            .Order()
            .ToList();
    }

    public Task Create(string tableName) =>
        _dbConnection.ExecuteAsync(
            $"CREATE TABLE \"{tableName}\" (id UUID NOT NULL DEFAULT uuid() PRIMARY KEY, data JSON NOT NULL)"
        );

    public Task<bool> Exists(string tableName) =>
        _dbConnection.QuerySingleAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_name = $TableName
            );
            """,
            new { TableName = tableName }
        );

    public void Dispose()
    {
        _dbConnection.Close();
        _dbConnection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbConnection.CloseAsync();
        await _dbConnection.DisposeAsync();
    }
}
