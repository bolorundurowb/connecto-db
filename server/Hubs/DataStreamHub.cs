using ConnectoDb.Server.Models.Data;
using ConnectoDb.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

[Authorize]
public class DataStreamHub(DataService dataService) : Hub
{
    public Task SubscribeToTable(string tableName) =>
        Groups.AddToGroupAsync(Context.ConnectionId, tableName);

    public Task UnsubscribeFromTable(string tableName) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, tableName);

    public async Task GetAllRecords(string tableName)
    {
        var records = await dataService.GetAll(tableName);
        await Clients.Caller.SendAsync(Config.EntitiesRequested, tableName, records);
    }

    public async Task GetRecord(string tableName, Guid id)
    {
        var record = await dataService.GetById(tableName, id);
        await Clients.Caller.SendAsync(Config.EntityRequested, tableName, record);
    }

    public async Task UpsertDataRecord(string tableName, FlexMap req)
    {
        if (req.HasId())
        {
            await dataService.Update(tableName, req);
            await Clients.Group(tableName).SendAsync(Config.EntityUpdated, tableName, req);
        }
        else
        {
            var id = await dataService.Create(tableName, req);
            var entity = await dataService.GetById(tableName, id);
            await Clients.Group(tableName).SendAsync(Config.EntityCreated, tableName, entity);
        }
    }

    public async Task DeleteRecord(string tableName, Guid id)
    {
        await dataService.Delete(tableName, id);
        await Clients.Group(tableName).SendAsync(Config.EntityDeleted, tableName, id);
    }
}
