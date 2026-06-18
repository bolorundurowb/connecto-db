using ConnectoDb.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

public class CollectionStreamHub(CollectionService collectionService) : Hub
{
    public async Task ListTables()
    {
        var tables = await collectionService.GetAll();
        await Clients.All.SendAsync(Config.TablesRequested, tables);
    }

    public async Task CreateTable(string tableName)
    {
        var tableExists = await collectionService.Exists(tableName);

        if (!tableExists)
        {
            await collectionService.Create(tableName);
            await Clients.All.SendAsync(Config.TableCreated, tableName);
        }
    }
}
