# SignalR Backplane Demo: Multi-Server Real-Time Messaging

This demo shows how to implement a **SignalR backplane** using Redis to enable real-time messaging across multiple server instances. Perfect for learning how to scale SignalR applications in production environments.

## What is SignalR?

**SignalR** is a library for ASP.NET Core that simplifies adding real-time web functionality to applications. It enables server-to-client communication in real-time, allowing you to push content to connected clients instantly.

**Key Features:**
- **Real-time communication** between server and clients
- **Automatic transport selection** (WebSockets, Server-Sent Events, Long Polling)
- **Connection management** and automatic reconnection
- **Cross-platform** support for .NET, JavaScript, and other clients

## What is a Backplane?

A **backplane** is a shared message bus that enables SignalR to work across multiple server instances. In a load-balanced environment, clients might connect to different servers, but the backplane ensures all clients receive messages regardless of which server they're connected to.

**Why You Need a Backplane:**
- **Load balancing** across multiple servers
- **High availability** and fault tolerance
- **Scalability** to handle more concurrent users
- **Message distribution** across all server instances

## Redis as a Backplane

**Redis** is the most popular backplane option for SignalR because it's:
- **Fast**: In-memory data store with high performance
- **Reliable**: Persistence and replication options
- **Simple**: Easy to set up and configure
- **Scalable**: Can handle high message throughput

## Prerequisites

Before you begin, ensure you have:

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [Node.js](https://nodejs.org/) (for Angular client)
- [Docker](https://www.docker.com/) (for Redis container)
- [Git](https://git-scm.com/) (to clone the repository)

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/joelbugarini/signalr-backplane-demo.git
cd signalr-backplane-demo
```

### 2. Start Redis (Backplane)

```bash
docker run -d -p 6379:6379 --name redis-backplane redis:latest
```

### 3. Run Multiple Server Instances

```bash
# Terminal 1 - Server 1
cd SignalRBackplaneDemo.Server
dotnet run --urls "http://localhost:5001"

# Terminal 2 - Server 2  
cd SignalRBackplaneDemo.Server
dotnet run --urls "http://localhost:5002"
```

### 4. Start the Angular Client

```bash
cd signalr-backplane-demo-client
npm install
npm start
```

### 5. Test the Backplane

1. Open http://localhost:4200 in your browser
2. Enter your name and send messages
3. Switch between Server 1 and Server 2 using the buttons
4. Notice that messages flow through the backplane regardless of which server you're connected to

## Step-by-Step Implementation

### Step 1: Create the ASP.NET Core Project

```bash
dotnet new webapi -n SignalRBackplaneDemo.Server
cd SignalRBackplaneDemo.Server
```

### Step 2: Add SignalR and Redis Packages

```bash
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

### Step 3: Create the SignalR Hub

Create `ChatHub.cs`:

```csharp
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
```

### Step 4: Configure SignalR with Redis Backplane

Update `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add CORS for client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add HttpContextAccessor for server identification
builder.Services.AddHttpContextAccessor();

// Configure SignalR with Redis backplane
var redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAngularClient");

// Map SignalR hub
app.MapHub<ChatHub>("/chatHub");

app.Run();
```

### Step 5: Create the Angular Client

```bash
ng new signalr-backplane-demo-client
cd signalr-backplane-demo-client
npm install @microsoft/signalr
```

### Step 6: Implement the SignalR Client

Update `app.ts`:

```typescript
import { Component, OnInit, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';

@Component({
  selector: 'app-root',
  template: `
    <div>
      <h1>SignalR Backplane Demo</h1>
      <div>Status: {{ isConnected ? 'Connected' : 'Disconnected' }}</div>
      <div>Server: {{ serverUrl }}</div>
      
      <div>
        <button (click)="switchServer('http://localhost:5001')">Server 1</button>
        <button (click)="switchServer('http://localhost:5002')">Server 2</button>
      </div>
      
      <div>
        <input [(ngModel)]="user" placeholder="Your name">
        <input [(ngModel)]="message" placeholder="Message">
        <button (click)="sendMessage()">Send</button>
      </div>
      
      <div>
        <div *ngFor="let msg of messages">{{ msg }}</div>
      </div>
    </div>
  `
})
export class App implements OnInit, OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;
  
  user = '';
  message = '';
  messages: string[] = [];
  isConnected = false;
  serverUrl = 'http://localhost:5001';

  ngOnInit() {
    this.connectToSignalR();
  }

  ngOnDestroy() {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

  connectToSignalR() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.serverUrl}/chatHub`)
      .build();

    // Listen for messages from ANY server (backplane magic!)
    this.hubConnection.on('ReceiveMessage', (user: string, message: string, serverInfo: string) => {
      const messageText = `[${serverInfo}] ${user}: ${message}`;
      this.messages.push(messageText);
    });

    this.hubConnection.start()
      .then(() => {
        this.isConnected = true;
        console.log(`Connected to SignalR hub at ${this.serverUrl}`);
      })
      .catch(err => {
        console.error('Error connecting to SignalR hub:', err);
        this.isConnected = false;
      });
  }

  sendMessage() {
    if (this.hubConnection && this.isConnected && this.user && this.message) {
      // Send to current server - backplane will distribute to all servers
      this.hubConnection.invoke('SendMessage', this.user, this.message)
        .catch(err => {
          console.error('Error sending message:', err);
        });
      
      this.message = '';
    }
  }

  switchServer(url: string) {
    this.serverUrl = url;
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
    this.connectToSignalR();
  }
}
```

## Docker Deployment

### Using Docker Compose

The project includes a `docker-compose.yml` file for easy deployment:

```bash
docker-compose up -d
```

This will start:
- **Redis backplane** on port 6379
- **Server 1** on port 5001
- **Server 2** on port 5002
- **Angular client** on port 4200

### Manual Docker Setup

```bash
# Start Redis
docker run -d -p 6379:6379 --name redis-backplane redis:latest

# Build and run servers
docker build -t signalr-backplane-demo-server ./SignalRBackplaneDemo.Server
docker run -d -p 5001:80 --name server1 signalr-backplane-demo-server
docker run -d -p 5002:80 --name server2 signalr-backplane-demo-server

# Build and run client
docker build -t signalr-backplane-demo-client ./signalr-backplane-demo-client
docker run -d -p 4200:80 --name client signalr-backplane-demo-client
```

## Configuration Options

### Redis Connection String

You can configure the Redis connection using:

**Environment Variable:**
```bash
export REDIS_CONNECTION="localhost:6379"
```

**appsettings.json:**
```json
{
  "RedisConnection": "localhost:6379"
}
```

**Docker Environment:**
```yaml
environment:
  - REDIS_CONNECTION=redis:6379
```

### CORS Configuration

Update the CORS policy in `Program.cs` for your specific domains:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://yourdomain.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

## How the Backplane Works

1. **Client Connection**: Client connects to any server instance
2. **Message Send**: Client sends message to connected server
3. **Redis Distribution**: Server publishes message to Redis
4. **Server Broadcast**: All servers receive message from Redis
5. **Client Delivery**: All clients receive message from their connected server

```
Client A → Server 1 → Redis → Server 2 → Client B
```

## Production Considerations

### Redis Configuration

For production, consider:
- **Redis Cluster** for high availability
- **Redis Sentinel** for automatic failover
- **Redis Persistence** for message durability
- **Redis Security** with authentication

### SignalR Configuration

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = "SignalR_";
        options.Configuration.DefaultDatabase = 0;
    });
```

### Monitoring and Logging

```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection)
    .AddHubOptions<ChatHub>(options =>
    {
        options.EnableDetailedErrors = true;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    });
```

## Troubleshooting

### Common Issues

**Connection Refused**
- Ensure Redis is running: `docker ps | grep redis`
- Check Redis connection string in configuration

**Messages Not Broadcasting**
- Verify all servers connect to the same Redis instance
- Check Redis logs: `docker logs redis-backplane`

**CORS Errors**
- Update CORS policy to include your client domain
- Ensure credentials are allowed for SignalR

**Performance Issues**
- Monitor Redis memory usage
- Consider Redis clustering for high throughput
- Implement message filtering if needed

## Next Steps

- **Explore Azure SignalR Service** for managed backplane
- **Implement authentication** with SignalR
- **Add message persistence** for offline clients
- **Scale to multiple regions** with Redis clustering
- **Monitor performance** with Application Insights

## Contributing

This is a demo project for learning SignalR backplane concepts. Feel free to:

- Report issues or bugs
- Suggest improvements
- Add new features
- Improve documentation

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. 