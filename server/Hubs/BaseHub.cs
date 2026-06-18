using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

namespace ConnectoDb.Server.Hubs;

public abstract partial class BaseHub : Hub
{
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex TableNameRegex();

    protected static bool IsValidTableName(string tableName) =>
        !string.IsNullOrWhiteSpace(tableName) && TableNameRegex().IsMatch(tableName);

    protected Task SendError(string message) =>
        Clients.Caller.SendAsync(Config.HubError, message);
}
