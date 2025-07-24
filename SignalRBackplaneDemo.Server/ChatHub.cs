using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
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
        // Get the server port from the current request
        var serverPort = _httpContextAccessor.HttpContext?.Request.Host.Port ?? 0;
        var serverInfo = $"Server {serverPort}";
        
        await Clients.All.SendAsync("ReceiveMessage", user, message, serverInfo);
    }

    public Task<string> GetReplicaId()
    {
        // Use the machine name as the replica identifier
        return Task.FromResult(System.Environment.MachineName);
    }
} 