namespace connecto.server.Models.Data;

public class User
{
    public Guid Id { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoggedInAt { get; private set; }

#pragma warning disable CS8618
    private User()
    {
    }
#pragma warning restore CS8618

    public User(string username, string password, string? firstName = null, string? lastName = null)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;

        Username = username;
        PasswordHash = HashText(password);
        FirstName = firstName;
        LastName = lastName;
    }

    public static string HashText(string text)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        return BCrypt.Net.BCrypt.HashPassword(text, salt);
    }
}