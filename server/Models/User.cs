namespace connecto.server.Models;

public class User
{
    public Guid Id { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string UserName { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoggedInAt { get; private set; }

    public User(string userName, string password, string? firstName = null, string? lastName = null)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;

        UserName = userName;
        PasswordHash = HashText(password);
        FirstName = firstName;
        LastName = lastName;
    }

    private static string HashText(string text)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        return BCrypt.Net.BCrypt.HashPassword(text, salt);
    }
}