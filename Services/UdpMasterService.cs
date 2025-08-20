



using System.Collections.Concurrent;
using Grpc.Core;
using UdpMaster;

namespace udp.master
{
    public class UdpMasterService(ILogger<UdpMasterService> logger) : UdpMaster.UdpMasterService.UdpMasterServiceBase
    {
        private readonly ILogger<UdpMasterService> _logger = logger;
        private readonly ConcurrentDictionary<string, IServerStreamWriter<DataProto>> _clients = new();

        public async override Task SendData(IAsyncStreamReader<DataProto> requestStream, IServerStreamWriter<DataProto> responseStream, ServerCallContext context)
        {
            string? clientName = context.RequestHeaders.FirstOrDefault(h => h.Key == "client-name")?.Value;
            string? serverName = context.RequestHeaders.FirstOrDefault(h => h.Key == "server-name")?.Value;

            if (clientName != null && _clients.TryAdd(clientName, responseStream))
            {
                _logger.LogInformation($"Client [{clientName}] connected to UdpMasterService.");

                try
                {
                    while (await requestStream.MoveNext())
                    {
                        _logger.LogInformation($"Received data from client {clientName}: {requestStream.Current.Data}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error processing data for client {clientName}: {ex.Message}");
                }

                _logger.LogInformation($"Client {clientName} disconnected from UdpMasterService.");
                _clients.TryRemove(clientName, out _);
            }
            else if (serverName != null)
            {
                _logger.LogInformation($"Server [{serverName}] already connected or name not provided.");

                try
                {
                    while (await requestStream.MoveNext())
                    {
                        ChunkData(requestStream.Current);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"Error processing data for server {serverName}: {ex.Message}");
                }

                _logger.LogInformation($"Server {serverName} disconnected from UdpMasterService.");
            }
            else
            {
                // không xác định được client hoặc server => ngắt kết nối
                _logger.LogInformation("Client or server name not provided.");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Client or server name not provided."));
            }
        }


        public async void ChunkData(DataProto data)
        {
            // _logger.LogInformation($"Send {data.Data.Length} bytes to {data.Ips.Count} clients by {_clients.Count} Node.");

            if (_clients.Count == 0)
                return;

            int chunk = data.Ips.Count / _clients.Count;
            int remainder = data.Ips.Count % _clients.Count;
            List<List<string>> ipChunks = new();

            for (int i = 0; i < _clients.Count; i++)
            {
                if (i == _clients.Count - 1)
                {
                    ipChunks.Add(data.Ips.Skip(i * chunk).Take(chunk + remainder).ToList());
                }
                else
                {
                    ipChunks.Add(data.Ips.Skip(i * chunk).Take(chunk).ToList());
                }
            }

            var keys = _clients.Keys.ToList();

            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[keys[i]];
                var clientData = new DataProto
                {
                    Data = data.Data
                };
                clientData.Ips.AddRange(ipChunks[i]);

                try
                {
                    await client.WriteAsync(clientData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to send data to client {keys[i]}: {ex.Message}");
                    // Có thể remove client nếu cần
                    _clients.TryRemove(keys[i], out _);
                }
            }
        }

    }
}

