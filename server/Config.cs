using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using connecto.server.Models.Data;
using dotenv.net.Utilities;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace connecto.server;

public static class Config
{
    public const string CoreDbName = "connecto-core.db";

    public const string UserDbNameTemplate = "connecto-user-{0}.db";


    private const string Issuer = "connecto db";

    private const string Audience = "connecto db";

    private static string Secret => EnvReader.GetStringValue("SECRET");


    public const string EntityUpdated = nameof(EntityUpdated);

    public const string EntityCreated = nameof(EntityCreated);

    public const string TablesRequested = nameof(TablesRequested);

    public const string TableCreated = nameof(TableCreated);

    internal static (string, DateTimeOffset) GenerateAuthToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("id", user.Id.ToString())
        };
        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);

        var token = new JwtSecurityToken
        (
            Issuer,
            Audience,
            claims,
            expires: expiresAt.DateTime,
            notBefore: DateTime.UtcNow,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
