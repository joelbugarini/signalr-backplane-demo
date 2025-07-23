using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public Task<string> GetReplicaId()
    {
        // Use the machine name as the replica identifier
        return Task.FromResult(System.Environment.MachineName);
    }
} 