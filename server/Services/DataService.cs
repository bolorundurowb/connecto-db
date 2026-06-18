using ConnectoDb.Server.Models.Data;
using Dapper;
using DuckDB.NET.Data;

namespace ConnectoDb.Server.Services;

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
        var entry = await _dbConnection.QuerySingleOrDefaultAsync<(Guid Id, string Data)>(
            $"SELECT id, data FROM \"{_tableName}\" WHERE id = $Id",
            new { Id = id }
        );

        if (entry == default)
            return null;

        return FlexMap.Deserialize(entry.Id, entry.Data);
    }

    public async Task<Guid> Create(FlexMap data) =>
        await _dbConnection.QuerySingleAsync<Guid>(
            $"INSERT INTO \"{_tableName}\"(data) VALUES ($Data) RETURNING id",
            new { Data = data.Serialize() });

    public async Task Update(FlexMap data)
    {
        if (!data.HasId())
            throw new InvalidOperationException("The provided data has no id");

        await _dbConnection.ExecuteAsync(
            $"UPDATE \"{_tableName}\" SET data = $Data WHERE id = $Id",
            new { Data = data.Serialize(), Id = data.Id() });
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
