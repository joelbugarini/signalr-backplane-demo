# SignalR Backplane Demo: Multi-Server Real-Time Messaging

## What is SignalR Backplane?

SignalR is a library for ASP.NET (and ASP.NET Core) that enables real-time web functionality. In a multi-server environment (e.g., load-balanced web servers), SignalR needs a way to coordinate messages between servers so that all clients receive messages, regardless of which server they're connected to. This coordination is called a backplane.

**Common backplane options:**
- Redis (most popular)
- Azure SignalR Service
- SQL Server

## Project Structure

This demo demonstrates:
- A single C# Web API server project with SignalR enabled and using a shared backplane (e.g., Redis).
- Multiple replicas (instances) of the server can be run, each connecting to the same Redis backplane.
- One Angular client that connects to the SignalR hub and can send/receive messages, regardless of which server instance it hits.

## High-Level Steps

1. **Set up Redis (as the backplane)**
2. **Create a single ASP.NET Core Web API project with SignalR, configured to use the Redis backplane**
3. **Run multiple replicas of the server (on different ports or machines)**
4. **Create a simple Angular client that connects to the SignalR hub**
5. **Run the servers and the client, and demonstrate cross-server messaging**

---

## Example: C# SignalR with Redis Backplane

### a. Prerequisites

- .NET 6+ SDK
- Node.js & Angular CLI
- Redis (local or Docker)

### b. Redis Setup (Docker example)

```bash
docker run -d -p 6379:6379 --name redis redis
```

---

### c. ASP.NET Core Web API with SignalR and Redis

#### 1. Create the project

```bash
dotnet new webapi -n SignalRBackplaneDemo.Server
cd SignalRBackplaneDemo.Server
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

#### 2. Add a SignalR Hub

Create a file `ChatHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
```

#### 3. Configure SignalR and Redis in `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR().AddStackExchangeRedis("localhost:6379");

var app = builder.Build();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<ChatHub>("/chatHub");
});

app.Run();
```

- Make sure all server replicas use the same Redis connection string.

#### 4. Run multiple server replicas on different ports

You can run multiple instances of the same server project on different ports to simulate replicas:

```bash
dotnet run --urls "http://localhost:5001"
dotnet run --urls "http://localhost:5002"
# (Add more as needed)
```

Each instance is a replica, and all are connected to the same Redis backplane.

---

### d. Angular Client

#### 1. Create Angular app

```bash
ng new signalr-backplane-demo-client
cd signalr-backplane-demo-client
npm install @microsoft/signalr
```

#### 2. Connect to SignalR Hub

In your Angular component:

```typescript
import { Component, OnInit } from '@angular/core';
import * as signalR from '@microsoft/signalr';

@Component({
  selector: 'app-root',
  template: `
    <input [(ngModel)]="user" placeholder="User" />
    <input [(ngModel)]="message" placeholder="Message" />
    <button (click)="sendMessage()">Send</button>
    <ul>
      <li *ngFor="let msg of messages">{{msg}}</li>
    </ul>
  `
})
export class AppComponent implements OnInit {
  private hubConnection: signalR.HubConnection;
  user = '';
  message = '';
  messages: string[] = [];

  ngOnInit() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5001/chatHub') // or 5002, 5003, etc.
      .build();

    this.hubConnection.on('ReceiveMessage', (user, message) => {
      this.messages.push(`${user}: ${message}`);
    });

    this.hubConnection.start();
  }

  sendMessage() {
    this.hubConnection.invoke('SendMessage', this.user, this.message);
    this.message = '';
  }
}
```

- You can switch the URL to any running server replica (5001, 5002, etc.) to test different instances.

---

## How the Backplane Works

- When a client sends a message to any server replica, SignalR uses Redis to publish the message.
- All other server replicas receive the message from Redis and forward it to their connected clients.
- This ensures all clients, regardless of which server instance they're connected to, receive the same messages.

---

## Summary

- **Backplane**: Redis is used to sync messages between server replicas.
- **Servers**: One ASP.NET Core Web API project, run as multiple replicas, each connected to Redis.
- **Client**: Angular app connects to any server replica and receives all messages. 

## Running Multiple Replicas Locally

To start two SignalR server replicas on ports 5001 and 5002 for local development, use the provided batch script:

### On Windows

1. Open a terminal in the project root directory.
2. Run:
   ```
   start.bat
   ```

This will launch two server instances, each on a different port, simulating multiple replicas for testing the SignalR backplane. 