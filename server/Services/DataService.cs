using ConnectoDb.Server.Data;
using ConnectoDb.Server.Models.Data;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1002 // Table names are quoted identifiers, not parameterizable

namespace ConnectoDb.Server.Services;

public class DataService(AppDbContext dbContext)
{
    private record DataRow(string Id, string Data);

    public async Task<List<FlexMap>> GetAll(string tableName)
    {
        var rows = await dbContext.Database
            .SqlQueryRaw<DataRow>($"SELECT id AS Id, data AS Data FROM \"{tableName}\"")
            .ToListAsync();

        return rows.Select(r => FlexMap.Deserialize(Guid.Parse(r.Id), r.Data)).ToList();
    }

    public async Task<FlexMap?> GetById(string tableName, Guid id)
    {
        var row = await dbContext.Database
            .SqlQueryRaw<DataRow>($"SELECT id AS Id, data AS Data FROM \"{tableName}\" WHERE id=@p0", id.ToString())
            .FirstOrDefaultAsync();

        if (row is null)
            return null;

        return FlexMap.Deserialize(Guid.Parse(row.Id), row.Data);
    }

    public async Task<Guid> Create(string tableName, FlexMap data)
    {
        var id = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{tableName}\"(id, data) VALUES (@p0, @p1)",
            id.ToString(), data.Serialize()
        );
        return id;
    }

    public async Task Update(string tableName, FlexMap data)
    {
        if (!data.HasId())
            throw new InvalidOperationException("The provided data has no id");

        await dbContext.Database.ExecuteSqlRawAsync(
            $"UPDATE \"{tableName}\" SET data=@p0 WHERE id=@p1",
            data.Serialize(), data.Id()!.Value.ToString()
        );
    }

    public Task Delete(string tableName, Guid id) =>
        dbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM \"{tableName}\" WHERE id=@p0",
            id.ToString()
        );
}

