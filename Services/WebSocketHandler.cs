using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using udp.master;
using UdpMaster;

public class WebSocketHandler
{
    private readonly WebSocketConnectionManager _manager;
    private readonly udp.master.UdpMasterService _udpMasterService;
    private const int BUFFER_SIZE = 4 * 1024;

    public WebSocketHandler(WebSocketConnectionManager manager, udp.master.UdpMasterService udpMasterService)
    {
        _manager = manager;
        _udpMasterService = udpMasterService;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        var socketId = _manager.AddSocket(socket);
        Console.WriteLine($"[WS] Connected: {socketId}");

        var buffer = new byte[BUFFER_SIZE];

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"[WS] Client requested close: {socketId}");
                    break;
                }

                // Handle binary messages only in this example
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var count = result.Count;
                    // support fragmented messages
                    using var ms = new MemoryStream();
                    ms.Write(buffer, 0, count);

                    while (!result.EndOfMessage)
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        ms.Write(buffer, 0, result.Count);
                    }

                    var dataByte = ms.ToArray();
                    DataProto dataProto = DataProto.Parser.ParseFrom(dataByte);
                    _udpMasterService.ChunkData(dataProto);
                    // var message = Encoding.UTF8.GetString(ms.ToArray());
                    // Console.WriteLine($"[WS] Received from {socketId}: {message}");

                    // Example: echo back to sender
                    // var echo = $"Echo from server: {DateTime.UtcNow:o} - {message}";
                    // await SendMessageAsync(socket, echo, ct);

                    // Optionally broadcast to everybody
                    // await BroadcastAsync($"Broadcast: {socketId} said: {message}", ct);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (WebSocketException wex)
        {
            Console.WriteLine($"[WS] WebSocketException for {socketId}: {wex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS] Exception for {socketId}: {ex}");
        }
        finally
        {
            _manager.RemoveSocket(socketId);
            Console.WriteLine($"[WS] Disconnected: {socketId}");
            try { socket.Dispose(); } catch { }
        }
    }

    public async Task SendMessageAsync(WebSocket socket, string message, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        var seg = new ArraySegment<byte>(bytes);
        await socket.SendAsync(seg, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct);
    }

    public async Task SendMessageByIdAsync(string id, string message, CancellationToken ct)
    {
        var socket = _manager.GetSocketById(id);
        if (socket != null) await SendMessageAsync(socket, message, ct);
    }

    public async Task BroadcastAsync(string message, CancellationToken ct)
    {
        var tasks = new System.Collections.Generic.List<Task>();
        foreach (var pair in _manager.GetAll())
        {
            if (pair.Value.State == WebSocketState.Open)
            {
                tasks.Add(SendMessageAsync(pair.Value, message, ct));
            }
        }
        await Task.WhenAll(tasks);
    }
}
