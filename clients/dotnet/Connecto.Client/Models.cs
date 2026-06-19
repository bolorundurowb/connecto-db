using System.Text.Json.Serialization;

namespace Connecto.Client;

public record UserRes(
    Guid Id,
    string? FirstName,
    string? LastName,
    string Username,
    DateTimeOffset CreatedAt
);

public record AuthRes(UserRes User, string Token, DateTimeOffset ExpiresAt);

public record LoginReq(string Username, string Password);

public record RegisterReq(
    string Username,
    string Password,
    string? FirstName = null,
    string? LastName = null
);

public record HubError(string Message);

public class FlexMap : Dictionary<string, object?>
{
    public FlexMap() : base() { }
    public FlexMap(IDictionary<string, object?> dictionary) : base(dictionary) { }
}
