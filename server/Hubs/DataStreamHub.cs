using ConnectoDb.Server.Models.Data;
using ConnectoDb.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

public class DataStreamHub(DataService dataService) : Hub
{
    public async Task UpsertDataRecord(string tableName, FlexMap req)
    {
        if (req.HasId())
        {
            await dataService.Update(tableName, req);
            await Clients.All.SendAsync(Config.EntityUpdated, tableName, req);
        }
        else
        {
            var id = await dataService.Create(tableName, req);
            var entity = await dataService.GetById(tableName, id);
            await Clients.All.SendAsync(Config.EntityCreated, tableName, entity);
        }
    }
}
