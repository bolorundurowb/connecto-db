using connecto.server.Models.Data;
using connecto.server.Models.Req;
using connecto.server.Models.Res;
using Dapper;
using DuckDB.NET.Data;
using Mapster;

namespace connecto.server.Services;

public class AuthService : IDisposable, IAsyncDisposable
{
    private readonly DuckDBConnection _dbConnection;

    public AuthService()
    {
        _dbConnection = new DuckDBConnection($"Data Source={Config.CoreDbName}");
        _dbConnection.Open();
        
        _dbConnection.Execute(
            """
             CREATE TABLE IF NOT EXISTS users (
                 id UUID NOT NULL DEFAULT uuid() PRIMARY KEY, 
                 first_name VARCHAR(256) NULL,
                 last_name VARCHAR(256) NULL,
                 user_name VARCHAR(1024) NOT NULL,
                 password_hash VARCHAR(2048) NOT NULL,
                 created_at TIMESTAMPTZ NOT NULL,
                 last_logged_in_at TIMESTAMPTZ NULL,
                 UNIQUE(user_name)
             )
             """
        );
    }

    public Task<User?> FindById(Guid userId) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE id = '{userId}'"
    );

    public Task<User?> FindByUsername(string username) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE user_name = '{username}'"
    );

    public async Task<AuthRes> Login(User user)
    {
        var loggedInAt = DateTimeOffset.UtcNow;
        await _dbConnection.ExecuteAsync($"UPDATE users SET last_logged_in_at = '{loggedInAt.ToString()}' WHERE id = '{user.Id}'");

        var userRes = user.Adapt<UserRes>();
        var (token, expiry) = Config.GenerateAuthToken(user);

        return new AuthRes(userRes, token, expiry);
    }

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
