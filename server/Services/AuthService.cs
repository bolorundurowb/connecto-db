using connecto.server.Models.Data;
using Dapper;
using DuckDB.NET.Data;

namespace connecto.server.Services;

public class AuthService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;

    public AuthService(string dbName)
    {
        _dbConnection = new DuckDBConnection($"Data Source={dbName}");
        _dbConnection.Open();
    }

    public Task<User?> FindByUsername(string username)
    {
        return _dbConnection.QuerySingleOrDefaultAsync<User>(
            $"SELECT * FROM users WHERE username = '{username}'"
        );
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