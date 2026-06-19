using ConnectoDb.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

[Authorize]
public class CollectionStreamHub(CollectionService collectionService) : BaseHub
{
    public async Task ListTables()
    {
        try
        {
            var tables = await collectionService.GetAll();
            await Clients.Caller.SendAsync(Config.TablesRequested, tables);
        }
        catch (Exception ex)
        {
            await SendError("Failed to list tables.", ex);
        }
    }

    public async Task CreateTable(string tableName)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        if (CollectionService.IsReserved(tableName))
        {
            await SendError($"'{tableName}' is a reserved name.");
            return;
        }

        try
        {
            var tableExists = await collectionService.Exists(tableName);
            if (tableExists)
            {
                await SendError($"Table '{tableName}' already exists.");
                return;
            }

            await collectionService.Create(tableName);
            await Clients.All.SendAsync(Config.TableCreated, tableName);
        }
        catch (Exception ex)
        {
            await SendError($"Failed to create table '{tableName}'.", ex);
        }
    }

    public async Task DeleteTable(string tableName)
    {
        if (!IsValidTableName(tableName))
        {
            await SendError($"Invalid table name: '{tableName}'");
            return;
        }

        if (CollectionService.IsReserved(tableName))
        {
            await SendError($"'{tableName}' is a reserved name.");
            return;
        }

        try
        {
            await collectionService.Delete(tableName);
            await Clients.All.SendAsync(Config.TableDeleted, tableName);
        }
        catch (Exception ex)
        {
            await SendError($"Failed to delete table '{tableName}'.", ex);
        }
    }
}
