namespace connecto.server.Models.Res;

public record AuthRes(UserRes User, string Token, DateTimeOffset ExpiresAt);
