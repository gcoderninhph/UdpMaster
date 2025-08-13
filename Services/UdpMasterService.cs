



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
            if (clientName != null && _clients.TryAdd(clientName, responseStream))
            {
                _logger.LogInformation($"Client [{clientName}] connected to UdpMasterService.");
            }
            else
            {
                _logger.LogInformation($"Client [{clientName}] already connected or name not provided.");
            }

            try
            {
                while (await requestStream.MoveNext())
                {
                    ChunkData(requestStream.Current);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error processing data for client {clientName}: {ex.Message}");
            }


            _logger.LogInformation($"Client {clientName} disconnected from UdpMasterService.");

            if (clientName != null) _clients.TryRemove(clientName, out _);
        }


        public async void ChunkData(DataProto data)
        {
            if (_clients.Count == 0)
                return;

            int chunk = data.Ips.Count / _clients.Count + 1;
            List<List<string>> ipChunks = new();

            for (int i = 0; i < _clients.Count; i++)
            {
                ipChunks.Add(data.Ips.Skip(i * chunk).Take(chunk).ToList());
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

