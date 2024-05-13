using connecto.server.Services;
using Microsoft.AspNetCore.SignalR;

namespace connecto.server.Hubs;

public class TableStreamHub : Hub
{
    private readonly TableService _tableService = new(Config.DbName);

    public async Task ListTables()
    {
        var tables = await _tableService.GetAll();
        await Clients.All.SendAsync(Config.TablesRequested, tables);
    }

    public async Task CreateTable(string tableName)
    {
        var tableExists = await _tableService.Exists(tableName);

        if (!tableExists)
        {
            await _tableService.Create(tableName);
            await Clients.All.SendAsync(Config.TableCreated, tableName);
        }
    }
}
