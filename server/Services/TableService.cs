using Dapper;
using DuckDB.NET.Data;

namespace connecto.server.Services;

public class TableService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;

    public TableService(string dbName)
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
            $"CREATE TABLE {tableName} (id UUID NOT NULL DEFAULT uuid() PRIMARY KEY, data JSON NOT NULL)"
        );

    public Task<bool> Exists(string tableName) =>
        _dbConnection.QuerySingleAsync<bool>(
            $"""
             SELECT EXISTS (
                 SELECT 1
                 FROM sqlite_master
                 WHERE type = 'table' AND name = '{tableName}'
             );
             """
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
