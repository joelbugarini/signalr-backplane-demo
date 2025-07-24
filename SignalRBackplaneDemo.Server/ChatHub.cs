using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;

public class ChatHub : Hub
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatHub(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SendMessage(string user, string message)
    {
        // Get the server port to demonstrate which server instance received the message
        var serverPort = _httpContextAccessor.HttpContext?.Request.Host.Port ?? 0;
        var serverInfo = $"Server {serverPort}";
        
        // Broadcast message to all clients with server info
        await Clients.All.SendAsync("ReceiveMessage", user, message, serverInfo);
    }
} 