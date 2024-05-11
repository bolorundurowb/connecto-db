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
}
