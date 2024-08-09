using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace TalkieServer.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;
        private static ConcurrentDictionary<string, string> _users = new ConcurrentDictionary<string, string>();

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var username = httpContext.Request.Query["username"].ToString();

            _users.TryAdd(username, Context.ConnectionId);

            _logger.LogInformation($"Client connected: {username}");
            await Clients.All.SendAsync("ReceiveMessage", "System", $"{username} connected.");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var username = _users.FirstOrDefault(u => u.Value == Context.ConnectionId).Key;
            _users.TryRemove(username, out _);

            _logger.LogInformation($"Client disconnected: {username}");
            await Clients.All.SendAsync("ReceiveMessage", "System", $"{username} disconnected.");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string user, string message)
        {
            _logger.LogInformation($"Message from {user}: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task SendPrivateMessage(string fromUser, string toUser, string message)
        {
            _logger.LogInformation($"Private message from {fromUser} to {toUser}: {message}");

            if (_users.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", fromUser, message);
            }
        }

        public async Task SendGroupMessage(string groupName, string user, string message)
        {
            _logger.LogInformation($"Group message from {user} to {groupName}: {message}");
            await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"{Context.ConnectionId} joined group {groupName}");
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"{Context.ConnectionId} left group {groupName}");
        }
    }
}
