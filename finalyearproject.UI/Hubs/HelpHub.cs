using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace finalyearproject.UI.Hubs
{
    public class HelpHub : Hub
    {
        public Task JoinUserGroup(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Task.CompletedTask;

            return Groups.AddToGroupAsync(Context.ConnectionId, "user-" + userId);
        }
    }
}
