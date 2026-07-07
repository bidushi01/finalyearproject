using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace finalyearproject.UI.Hubs
{
    public class NameUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
