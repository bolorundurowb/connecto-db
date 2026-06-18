using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ConnectoDb.Server.Models.Data;
using dotenv.net.Utilities;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace ConnectoDb.Server;

public static class Config
{
    public const string CoreDbName = "connecto-core.db";

    private const string Issuer = "connecto db";

    private const string Audience = "connecto db";

    private static string Secret => EnvReader.GetStringValue("SECRET");

    // Data record events
    public const string EntityCreated = nameof(EntityCreated);
    public const string EntityUpdated = nameof(EntityUpdated);
    public const string EntityDeleted = nameof(EntityDeleted);
    public const string EntityRequested = nameof(EntityRequested);
    public const string EntitiesRequested = nameof(EntitiesRequested);

    // Collection events
    public const string TablesRequested = nameof(TablesRequested);
    public const string TableCreated = nameof(TableCreated);
    public const string TableDeleted = nameof(TableDeleted);

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
