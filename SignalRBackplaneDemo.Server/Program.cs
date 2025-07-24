using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add CORS for Angular client
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

// Add HttpContextAccessor for ChatHub
builder.Services.AddHttpContextAccessor();

// Bind SignalRConfiguration section
builder.Services.Configure<SignalRConfiguration>(builder.Configuration.GetSection("SignalRConfiguration"));

// Configure SignalR with RabbitMQ backplane (demo/testing only)
builder.Services.AddSignalR().AddRabbitMQ();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAngularClient");

// Map SignalR hub
app.MapHub<ChatHub>("/chatHub");

app.Run();

// Extension method for AddRabbitMQ
public static class SignalRBuilderExtensions
{
    public static ISignalRServerBuilder AddRabbitMQ(this ISignalRServerBuilder builder)
    {
        builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(RabbitMqHubLifetimeManager<>));
        return builder;
    }
}
 