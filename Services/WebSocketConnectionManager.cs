using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public string AddSocket(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString();
        _sockets.TryAdd(id, socket);
        return id;
    }

    public bool RemoveSocket(string id)
    {
        if (_sockets.TryRemove(id, out var socket))
        {
            try { socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None).Wait(); }
            catch { }
            return true;
        }
        return false;
    }

    public WebSocket? GetSocketById(string id) =>
        _sockets.TryGetValue(id, out var socket) ? socket : null;

    public ConcurrentDictionary<string, WebSocket> GetAll() => _sockets;
}
