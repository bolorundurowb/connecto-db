using connecto.server.Services;
using Microsoft.AspNetCore.SignalR;

namespace connecto.server.Hubs;

public class CollectionStreamHub : Hub
{
    private readonly CollectionService _collectionService = new(Config.DbName);

    public async Task ListTables()
    {
        var tables = await _collectionService.GetAll();
        await Clients.All.SendAsync(Config.TablesRequested, tables);
    }

    public async Task CreateTable(string tableName)
    {
        var tableExists = await _collectionService.Exists(tableName);

        if (!tableExists)
        {
            await _collectionService.Create(tableName);
            await Clients.All.SendAsync(Config.TableCreated, tableName);
        }
    }
}
