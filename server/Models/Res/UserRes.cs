namespace connecto.server.Models.Res;

public class UserRes
{
    public Guid Id { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string Username { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
