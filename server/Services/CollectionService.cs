using ConnectoDb.Server.Data;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1002 // Table names are quoted identifiers, not parameterizable

namespace ConnectoDb.Server.Services;

public class CollectionService(AppDbContext dbContext)
{
    private static readonly HashSet<string> ReservedNames =
        ["users", "__EFMigrationsHistory", "sqlite_sequence", "sqlite_master"];

    public static bool IsReserved(string tableName) =>
        ReservedNames.Contains(tableName, StringComparer.OrdinalIgnoreCase);

    public async Task<List<string>> GetAll()
    {
        var excluded = string.Join(", ", ReservedNames.Select(t => $"'{t}'"));
        var tables = await dbContext.Database
            .SqlQueryRaw<string>($"SELECT name AS Value FROM sqlite_master WHERE type='table' AND name NOT IN ({excluded})")
            .ToListAsync();
        return [..tables.Order()];
    }

    public Task Create(string tableName)
    {
        if (IsReserved(tableName))
            throw new InvalidOperationException($"'{tableName}' is a reserved name and cannot be used.");

        return dbContext.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE \"{tableName}\" (id TEXT NOT NULL PRIMARY KEY, data TEXT NOT NULL)"
        );
    }

    public async Task<bool> Exists(string tableName)
    {
        var count = await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name=@p0", tableName)
            .FirstAsync();
        return count > 0;
    }

    public Task Delete(string tableName)
    {
        if (IsReserved(tableName))
            throw new InvalidOperationException($"'{tableName}' is a reserved name and cannot be dropped.");

        return dbContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{tableName}\"");
    }
}

