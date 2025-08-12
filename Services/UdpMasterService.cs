



using System.Collections.Concurrent;
using Grpc.Core;
using UdpMaster;

namespace udp.master
{
    public class UdpMasterService : UdpMaster.UdpMasterService.UdpMasterServiceBase
    {
        private readonly ConcurrentDictionary<string, IServerStreamWriter<DataProto>> _clients = new();

        public async override Task SendData(IAsyncStreamReader<DataProto> requestStream, IServerStreamWriter<DataProto> responseStream, ServerCallContext context)
        {
            string? clientName = context.RequestHeaders.FirstOrDefault(h => h.Key == "client-name")?.Value;
            if (clientName != null && _clients.TryAdd(clientName, responseStream))
            {
                Console.WriteLine($"Client [{clientName}] connected to UdpMasterService.");
            }
            else
            {
                Console.WriteLine($"Client [{clientName}] already connected or name not provided.");
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
                Console.WriteLine($"Error processing data for client {clientName}: {ex.Message}");
            }


            Console.WriteLine($"Client {clientName} disconnected from UdpMasterService.");

            if (clientName != null) _clients.TryRemove(clientName, out _);
        }


        public async void ChunkData(DataProto data)
        {
            int chunk = data.Ips.Count / _clients.Count + 1;
            List<List<string>> ipChunks = new();

            for (int i = 0; i < _clients.Count; i++)
            {
                ipChunks.Add([.. data.Ips.Skip(i * chunk).Take(chunk)]);
            }

            foreach (var client in _clients)
            {
                var clientData = new DataProto
                {
                    Data = data.Data
                };
                clientData.Ips.AddRange(ipChunks[_clients.Keys.ToList().IndexOf(client.Key)]);
                await client.Value.WriteAsync(clientData);
            }
        }
    }
}

