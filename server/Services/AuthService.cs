using connecto.server.Models.Data;
using connecto.server.Models.Req;
using Dapper;
using DuckDB.NET.Data;

namespace connecto.server.Services;

public class AuthService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;

    public AuthService()
    {
        _dbConnection = new DuckDBConnection($"Data Source={Config.CoreDbName}");
        _dbConnection.Open();
    }

    public Task<User?> FindById(Guid userId) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE id = '{userId}'"
    );

    public Task<User?> FindByUsername(string username) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE username = '{username}'"
    );

    public async Task<User> Create(RegisterReq details)
    {
        var userId = await _dbConnection.QuerySingleAsync<Guid>(
            $"INSERT INTO users (FirstName, LastName, Username, PasswordHash) VALUES (@FirstName, @LastName, @Username, @PasswordHash)",
            new
            {
                details.Username,
                details.FirstName,
                details.LastName,
                PasswordHash = User.HashText(details.Password),
            }
        );
        return (await FindById(userId))!;
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
