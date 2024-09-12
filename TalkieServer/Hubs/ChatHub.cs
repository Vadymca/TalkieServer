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
            try
            {
                var httpContext = Context.GetHttpContext();
                var username = httpContext.Request.Query["username"].ToString();

                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.LogWarning("Username is null or empty during connection.");
                    throw new ArgumentNullException("Username cannot be null or empty.");
                }

                _users.TryAdd(username, Context.ConnectionId);

                _logger.LogInformation($"Client connected: {username}");
                await Clients.All.SendAsync("UserOnline", username);
                await Clients.All.SendAsync("ReceiveMessage", "System", $"{username} connected.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnConnectedAsync: {ex.Message}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                var username = _users.FirstOrDefault(u => u.Value == Context.ConnectionId).Key;

                if (username != null)
                {
                    _users.TryRemove(username, out _);

                    _logger.LogInformation($"Client disconnected: {username}");
                    await Clients.All.SendAsync("UserOffline", username);
                    await Clients.All.SendAsync("ReceiveMessage", "System", $"{username} disconnected.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnDisconnectedAsync: {ex.Message}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string user, string message)
        {
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SendMessage called with null or empty user or message.");
                return;
            }

            _logger.LogInformation($"Message from {user}: {message}");
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task SendPrivateMessage(string fromUser, string toUser, string message)
        {
            if (string.IsNullOrWhiteSpace(fromUser) || string.IsNullOrWhiteSpace(toUser) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SendPrivateMessage called with null or empty parameters.");
                return;
            }

            _logger.LogInformation($"Private message from {fromUser} to {toUser}: {message}");

            if (_users.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceivePrivateMessage", fromUser, message);
            }
            else
            {
                _logger.LogWarning($"User {toUser} not found for private message.");
            }
        }

        public async Task SendGroupMessage(string groupName, string user, string message)
        {
            if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("SendGroupMessage called with null or empty parameters.");
                return;
            }

            _logger.LogInformation($"Group message from {user} to {groupName}: {message}");
            await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
        }

        public async Task JoinGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                _logger.LogWarning("JoinGroup called with null or empty groupName.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"{Context.ConnectionId} joined group {groupName}");
        }

        public async Task LeaveGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                _logger.LogWarning("LeaveGroup called with null or empty groupName.");
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"{Context.ConnectionId} left group {groupName}");
        }

       
        public async Task SendFile(string fileName, byte[] fileContent, string recipient = null)
        {
            if (fileContent == null || fileContent.Length == 0)
            {
                _logger.LogWarning("SendFile called with null or empty file content.");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("SendFile called with null or empty fileName.");
                return;
            }

            _logger.LogInformation($"File {fileName} sent.");

            if (!string.IsNullOrEmpty(recipient) && _users.TryGetValue(recipient, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveFile", fileName, fileContent);
            }
            else
            {
                await Clients.All.SendAsync("ReceiveFile", fileName, fileContent);
            }
        }
    }
}
