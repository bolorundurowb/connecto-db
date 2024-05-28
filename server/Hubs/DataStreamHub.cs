using connecto.server.Models.Data;
using connecto.server.Services;
using Microsoft.AspNetCore.SignalR;

namespace connecto.server.Hubs;

public class DataStreamHub : Hub
{
    private readonly Dictionary<string, DataService> _services = new();

    public async Task UpsertDataRecord(string tableName, FlexMap req)
    {
        var service = GetServiceForTable(tableName);

        if (req.HasId())
        {
            await service.Update(req);
            await Clients.All.SendAsync(Config.EntityUpdated, tableName, req);
        }
        else
        {
            var id = await service.Create(req);
            var entity = await service.GetById(id);
            await Clients.All.SendAsync(Config.EntityCreated, tableName, entity);
        }
    }

    private DataService GetServiceForTable(string tableName)
    {
        var hasKey = _services.ContainsKey(tableName);

        if (hasKey)
            return _services[tableName];

        var service = new DataService(Config.CoreDbName, tableName);
        _services.Add(tableName, service);

        return service;
    }
}
