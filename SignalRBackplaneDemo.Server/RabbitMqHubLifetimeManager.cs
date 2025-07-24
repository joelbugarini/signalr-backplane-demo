using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Protocol;

public class RabbitMqHubLifetimeManager<THub> : HubLifetimeManager<THub>, IDisposable where THub : Hub
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "signalr_backplane";
    private readonly Channel<(string method, object[] args)> _localMessages = Channel.CreateUnbounded<(string, object[])>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _localBroadcastTask;
    private readonly ConcurrentDictionary<string, HubConnectionContext> _connections = new();

    public RabbitMqHubLifetimeManager(IOptions<SignalRConfiguration> config)
    {
        var connectionString = config.Value.RabbitMQ?.ConnectionString ?? "amqp://guest:guest@localhost:5672/";
        var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Fanout, durable: false);

        // Subscribe to the exchange
        var queueName = _channel.QueueDeclare().QueueName;
        _channel.QueueBind(queueName, _exchangeName, "");
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var msg = JsonSerializer.Deserialize<BackplaneMessage>(body);
            if (msg != null)
            {
                // Enqueue for local broadcast
                await _localMessages.Writer.WriteAsync((msg.Method, msg.Args));
            }
        };
        _channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

        // Start local broadcast loop
        _localBroadcastTask = Task.Run(LocalBroadcastLoop);
    }

    public override Task OnConnectedAsync(HubConnectionContext connection)
    {
        _connections[connection.ConnectionId] = connection;
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        _connections.TryRemove(connection.ConnectionId, out _);
        return Task.CompletedTask;
    }

    public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        // Only publish to RabbitMQ, do NOT broadcast locally here
        var msg = new BackplaneMessage { Method = methodName, Args = args };
        var body = JsonSerializer.SerializeToUtf8Bytes(msg);
        _channel.BasicPublish(exchange: _exchangeName, routingKey: "", body: body);
        return Task.CompletedTask;
    }

    public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Local broadcast loop: send received messages to all local clients
    private async Task LocalBroadcastLoop()
    {
        await foreach (var (method, args) in _localMessages.Reader.ReadAllAsync(_cts.Token))
        {
            foreach (var connection in _connections.Values)
            {
                await connection.WriteAsync(new InvocationMessage(method, args));
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _localBroadcastTask.Wait();
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
        {
            // Ignore TaskCanceledExceptions on shutdown
        }
        _channel?.Dispose();
        _connection?.Dispose();
    }

    private class BackplaneMessage
    {
        public string Method { get; set; }
        public object[] Args { get; set; }
    }
}

// Add POCOs for config
public class SignalRConfiguration
{
    public bool EnableDetailedErrors { get; set; }
    public bool UseRedisBackplane { get; set; }
    public bool UseRabbitMqBackplane { get; set; }
    public RabbitMqOptions RabbitMQ { get; set; }
    public RedisOptions StackExchangeRedis { get; set; }
}
public class RabbitMqOptions
{
    public string ConnectionString { get; set; }
}
public class RedisOptions
{
    public bool AbortOnConnectFail { get; set; }
    public string Password { get; set; }
    public string RedisServerEndpoint { get; set; }
} 