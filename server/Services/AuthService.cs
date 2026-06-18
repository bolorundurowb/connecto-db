using ConnectoDb.Server.Data;
using ConnectoDb.Server.Models.Data;
using ConnectoDb.Server.Models.Req;
using ConnectoDb.Server.Models.Res;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ConnectoDb.Server.Services;

public class AuthService(AppDbContext dbContext)
{
    public Task<User?> FindById(Guid userId) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

    public Task<User?> FindByUsername(string username) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);

    public async Task<AuthRes> Login(User user)
    {
        var loggedInAt = DateTimeOffset.UtcNow;
        await dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoggedInAt, loggedInAt));

        var userRes = user.Adapt<UserRes>();
        var (token, expiry) = Config.GenerateAuthToken(user);

        return new AuthRes(userRes, token, expiry);
    }

    public async Task<User> Create(RegisterReq details)
    {
        var user = new User(details.Username, details.Password, details.FirstName, details.LastName);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}

