using ConnectoDb.Server.Models.Data;
using ConnectoDb.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

[Authorize]
public class DataStreamHub(DataService dataService) : BaseHub
{
    public async Task SubscribeToTable(string tableName)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tableName);
    }

    public async Task UnsubscribeFromTable(string tableName)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tableName);
    }

    public async Task GetAllRecords(string tableName)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        try
        {
            var records = await dataService.GetAll(tableName);
            await Clients.Caller.SendAsync(Config.EntitiesRequested, tableName, records);
        }
        catch (Exception ex)
        {
            await SendError(ex.Message);
        }
    }

    public async Task GetRecord(string tableName, Guid id)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        try
        {
            var record = await dataService.GetById(tableName, id);
            await Clients.Caller.SendAsync(Config.EntityRequested, tableName, record);
        }
        catch (Exception ex)
        {
            await SendError(ex.Message);
        }
    }

    public async Task UpsertDataRecord(string tableName, FlexMap req)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        try
        {
            if (req.HasId())
            {
                var updated = await dataService.Update(tableName, req);
                await Clients.Group(tableName).SendAsync(Config.EntityUpdated, tableName, updated);
            }
            else
            {
                var id = await dataService.Create(tableName, req);
                var entity = await dataService.GetById(tableName, id);
                await Clients.Group(tableName).SendAsync(Config.EntityCreated, tableName, entity);
            }
        }
        catch (Exception ex)
        {
            await SendError(ex.Message);
        }
    }

    public async Task DeleteRecord(string tableName, Guid id)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        try
        {
            await dataService.Delete(tableName, id);
            await Clients.Group(tableName).SendAsync(Config.EntityDeleted, tableName, id);
        }
        catch (Exception ex)
        {
            await SendError(ex.Message);
        }
    }
}
