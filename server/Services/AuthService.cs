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
                firstname VARCHAR(256) NULL,
                lastname VARCHAR(256) NULL,
                username VARCHAR(1024) NOT NULL,
                passwordhash VARCHAR(2048) NOT NULL,
                createdat TIMESTAMPTZ NOT NULL,
                lastloggedinat TIMESTAMPTZ NULL,
                UNIQUE(username)
            )
            """
        );
    }

    public Task<User?> FindById(Guid userId) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE id = '{userId}'"
    );

    public Task<User?> FindByUsername(string username) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        $"SELECT * FROM users WHERE username = '{username}'"
    );

    public async Task<AuthRes> Login(User user)
    {
        var loggedInAt = DateTimeOffset.UtcNow;
        await _dbConnection.ExecuteAsync($"UPDATE users SET lastloggedinat = '{loggedInAt:o}' WHERE id = '{user.Id}'");

        var userRes = user.Adapt<UserRes>();
        var (token, expiry) = Config.GenerateAuthToken(user);

        return new AuthRes(userRes, token, expiry);
    }

    public async Task<User> Create(RegisterReq details)
    {
        var userId = await _dbConnection.QuerySingleAsync<Guid>(
            $"""
             INSERT INTO users (firstname, lastname, username, passwordhash, createdat) 
             VALUES (
                     '{details.FirstName}',
                     '{details.LastName}',
                     '{details.Username}',
                     '{User.HashText(details.Password)}',
                     '{DateTimeOffset.UtcNow:o}'
              ) RETURNING id
             """
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
