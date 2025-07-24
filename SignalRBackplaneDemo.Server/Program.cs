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

// Configure SignalR with Redis backplane
var redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors("AllowAngularClient");

// Map SignalR hub
app.MapHub<ChatHub>("/chatHub");

app.Run();
 