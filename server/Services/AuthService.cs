using ConnectoDb.Server.Models.Data;
using ConnectoDb.Server.Models.Req;
using ConnectoDb.Server.Models.Res;
using Dapper;
using DuckDB.NET.Data;
using Mapster;

namespace ConnectoDb.Server.Services;

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
        "SELECT * FROM users WHERE id = $Id",
        new { Id = userId }
    );

    public Task<User?> FindByUsername(string username) => _dbConnection.QuerySingleOrDefaultAsync<User>(
        "SELECT * FROM users WHERE username = $Username",
        new { Username = username }
    );

    public async Task<AuthRes> Login(User user)
    {
        var loggedInAt = DateTimeOffset.UtcNow;
        await _dbConnection.ExecuteAsync(
            "UPDATE users SET lastloggedinat = $LoggedInAt WHERE id = $Id",
            new { LoggedInAt = loggedInAt, Id = user.Id }
        );

        var userRes = user.Adapt<UserRes>();
        var (token, expiry) = Config.GenerateAuthToken(user);

        return new AuthRes(userRes, token, expiry);
    }

    public async Task<User> Create(RegisterReq details)
    {
        var userId = await _dbConnection.QuerySingleAsync<Guid>(
            """
            INSERT INTO users (firstname, lastname, username, passwordhash, createdat) 
            VALUES ($FirstName, $LastName, $Username, $PasswordHash, $CreatedAt) RETURNING id
            """,
            new
            {
                details.FirstName,
                details.LastName,
                details.Username,
                PasswordHash = User.HashText(details.Password),
                CreatedAt = DateTimeOffset.UtcNow
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
