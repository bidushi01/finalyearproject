using System.Security.Claims;
using finalyearproject.Data.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace finalyearproject.UI.Hubs
{
    [Authorize(Roles = "User")]
    public class HelpHub : Hub
    {
        private readonly IUserRepository _userRepository;

        public HelpHub(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public Task JoinUserGroup(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Task.CompletedTask;

            return Groups.AddToGroupAsync(Context.ConnectionId, "user-" + userId);
        }

        public async Task SendVideoCallInvite(int helpRequestId, string targetUserId, string callerName)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null || !await CanJoinVideoCallAsync(helpRequestId, fromUserId.Value))
                throw new HubException("Cannot start a video call for this session.");

            await Clients.User(targetUserId)
                .SendAsync("VideoCallInvite", fromUserId.Value.ToString(), helpRequestId, callerName ?? "Someone");
        }

        public async Task RespondVideoCallInvite(int helpRequestId, string callerUserId, bool accepted)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null || !await CanJoinVideoCallAsync(helpRequestId, fromUserId.Value))
                return;

            if (accepted)
            {
                await Clients.User(callerUserId)
                    .SendAsync("VideoCallAccepted", fromUserId.Value.ToString(), helpRequestId);
            }
            else
            {
                await Clients.User(callerUserId)
                    .SendAsync("VideoCallDeclined", fromUserId.Value.ToString(), helpRequestId);
            }
        }

        public async Task CancelVideoCallInvite(int helpRequestId, string targetUserId)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null) return;

            await Clients.User(targetUserId)
                .SendAsync("VideoCallCancelled", fromUserId.Value.ToString(), helpRequestId);
        }

        public async Task JoinVideoCall(int helpRequestId)
        {
            var userId = GetUserId();
            if (userId == null)
                throw new HubException("Not signed in on the real-time connection. Refresh the page and try again.");
            if (!await CanJoinVideoCallAsync(helpRequestId, userId.Value))
                throw new HubException("You cannot join this video call. The help request must be Accepted.");

            await Groups.AddToGroupAsync(Context.ConnectionId, VideoGroup(helpRequestId));
            await Clients.OthersInGroup(VideoGroup(helpRequestId))
                .SendAsync("PeerCallReconnecting", userId.Value.ToString(), helpRequestId);
        }

        public async Task LeaveVideoCall(int helpRequestId)
        {
            var userId = GetUserId();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, VideoGroup(helpRequestId));
            if (userId != null)
            {
                await Clients.OthersInGroup(VideoGroup(helpRequestId))
                    .SendAsync("CallPeerLeft", userId.Value.ToString());
            }
        }

        public async Task SendVideoOffer(int helpRequestId, string targetUserId, string sdp)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null || !await CanJoinVideoCallAsync(helpRequestId, fromUserId.Value))
                return;

            await Clients.User(targetUserId)
                .SendAsync("ReceiveVideoOffer", fromUserId.Value.ToString(), helpRequestId, sdp);
        }

        public async Task SendVideoAnswer(int helpRequestId, string targetUserId, string sdp)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null || !await CanJoinVideoCallAsync(helpRequestId, fromUserId.Value))
                return;

            await Clients.User(targetUserId)
                .SendAsync("ReceiveVideoAnswer", fromUserId.Value.ToString(), helpRequestId, sdp);
        }

        public async Task SendIceCandidate(int helpRequestId, string targetUserId, string candidate, string sdpMid, int? sdpMLineIndex)
        {
            var fromUserId = GetUserId();
            if (fromUserId == null || !await CanJoinVideoCallAsync(helpRequestId, fromUserId.Value))
                return;

            await Clients.User(targetUserId)
                .SendAsync("ReceiveIceCandidate", fromUserId.Value.ToString(), helpRequestId, candidate, sdpMid, sdpMLineIndex);
        }

        private int? GetUserId()
        {
            var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : null;
        }

        private static string VideoGroup(int helpRequestId) => "video-" + helpRequestId;

        private async Task<bool> CanJoinVideoCallAsync(int helpRequestId, int userId)
        {
            var parties = await _userRepository.GetHelpRequestPartiesAsync(helpRequestId);
            if (parties.SeekerId == 0 || parties.HelperId == 0)
                return false;

            if (userId != parties.SeekerId && userId != parties.HelperId)
                return false;

            var status = (await _userRepository.GetHelpRequestStatusAsync(helpRequestId) ?? "")
                .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();

            return status == "accepted";
        }
    }
}
