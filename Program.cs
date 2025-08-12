using GrpcDemo.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using udp.master;
using udp.node;

var builder = WebApplication.CreateBuilder(args);

// tạm bỏ qua tls
builder.WebHost.ConfigureKestrel(options =>
{
    // Cổng cho gRPC
    options.ListenLocalhost(5000, o =>
    {
        o.Protocols = HttpProtocols.Http2; // gRPC bắt buộc HTTP/2
    });

    // Cổng cho WebSocket
    options.ListenLocalhost(5001, o =>
    {
        o.Protocols = HttpProtocols.Http1; // WebSocket cần HTTP/1.1
    });
});

builder.Services.AddGrpc();
builder.Services.AddSingleton<IUdpSenderService, UdpSenderService>();
builder.Services.AddHostedService<UdpNodeService>();
builder.Services.AddSingleton<UdpMasterService>();
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();


app.MapGrpcService<GreeterService>();
app.MapGrpcService<UdpMasterService>();
app.MapGet("/", () => "Đây là gRPC server. Hãy dùng client để kết nối.");

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// WebSocket endpoint
app.Map("/ws", async (HttpContext context, WebSocketConnectionManager manager, WebSocketHandler handler) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var ct = context.RequestAborted;
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await handler.HandleAsync(webSocket, ct);
});


app.Run();
