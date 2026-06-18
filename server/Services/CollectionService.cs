using ConnectoDb.Server.Data;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1002 // Table names are quoted identifiers, not parameterizable

namespace ConnectoDb.Server.Services;

public class CollectionService(AppDbContext dbContext)
{
    private static readonly string[] SystemTables = ["users", "__EFMigrationsHistory", "sqlite_sequence"];

    public async Task<List<string>> GetAll()
    {
        var excluded = string.Join(", ", SystemTables.Select(t => $"'{t}'"));
        var tables = await dbContext.Database
            .SqlQueryRaw<string>($"SELECT name AS Value FROM sqlite_master WHERE type='table' AND name NOT IN ({excluded})")
            .ToListAsync();
        return [..tables.Order()];
    }

    public Task Create(string tableName) =>
        dbContext.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE \"{tableName}\" (id TEXT NOT NULL PRIMARY KEY, data TEXT NOT NULL)"
        );

    public Task<bool> Exists(string tableName)
    {
        // intentional: count > 0 check
        return dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name=@p0", tableName)
            .FirstAsync()
            .ContinueWith(t => t.Result > 0);
    }

    public Task Delete(string tableName) =>
        dbContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{tableName}\"");
}

