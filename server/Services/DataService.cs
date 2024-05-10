using System.Text.Json;
using connecto.server.Models;
using Dapper;
using DuckDB.NET.Data;

namespace connecto.server.Services;

public class DataService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;
    private readonly string _tableName;

    public DataService(string dbName, string tableName)
    {
        _tableName = tableName;
        _dbConnection = new DuckDBConnection($"Data Source={dbName}");
        _dbConnection.Open();
    }

    public async Task<FlexMap?> GetById(Guid id)
    {
        await Utils.EnsureTableCreated(_dbConnection, _tableName);
        var entry = await _dbConnection.QuerySingleAsync<(Guid Id, string Data)>(
            $"SELECT id, data FROM {_tableName} WHERE id='{id}'"
        );

        return FlexMap.Deserialize(entry.Id, entry.Data);
    }

    public async Task<Guid> Create(FlexMap data)
    {
        await Utils.EnsureTableCreated(_dbConnection, _tableName);
        return await _dbConnection.QuerySingleAsync<Guid>(
            $"INSERT INTO {_tableName}(data) VALUES ('{data.Serialize()}') RETURNING id");
    }

    public async Task Update(FlexMap data)
    {
        await Utils.EnsureTableCreated(_dbConnection, _tableName);

        if (!data.HasId())
            throw new InvalidOperationException("The provided data has no id");

        await _dbConnection.ExecuteAsync(
            $"UPDATE {_tableName}(data) VALUES ('{data.Serialize()}') WHERE id='{data.Id()}'");
    }

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
