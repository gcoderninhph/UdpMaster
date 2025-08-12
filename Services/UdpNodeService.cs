using Grpc.Core;
using Grpc.Net.Client;
using udp.master;
using UdpMaster;

namespace udp.node
{
    public class UdpNodeService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<UdpNodeService> _logger;
        public string[] otherNodes = [];
        private readonly string id;
        private readonly IUdpSenderService _udpSenderService;

        public UdpNodeService(IConfiguration config, ILogger<UdpNodeService> logger, IUdpSenderService udpSenderService)
        {
            _config = config;
            _logger = logger;
            _udpSenderService = udpSenderService;
            id = Guid.NewGuid().ToString();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UdpNodeService initialized.");
            var value = _config.GetSection("UdpNode").Get<string[]>();
            otherNodes = value ?? [];

            if (otherNodes.Length == 0)
            {
                _logger.LogWarning("UdpNode configuration is empty. Please provide at least one node address.");
            }
            else
            {
                foreach (var node in otherNodes)
                {
                    ConnectWithRetryAsync(node);
                }
            }
            return Task.CompletedTask;
        }


        async void ConnectWithRetryAsync(string node)
        {
            while (true) // Thử lại vĩnh viễn
            {
                try
                {
                    Console.WriteLine($"[INFO] Đang kết nối tới {node}...");

                    var channel = GrpcChannel.ForAddress(node);
                    var client = new UdpMaster.UdpMasterService.UdpMasterServiceClient(channel);
                    // set metadata
                    var headers = new Metadata
                    {
                        { "client-name", id }
                    };
                    // Mở stream
                    using var call = client.SendData(headers);
                    Console.WriteLine($"[OK] Kết nối thành công tới {node}");

                    // Task đọc dữ liệu từ server
                    await foreach (var response in call.ResponseStream.ReadAllAsync())
                    {
                        string[] ips = [.. response.Ips];
                        var data = response.Data.ToByteArray();
                        _udpSenderService.Send(ips, data);
                    }

                    // Nếu ReadAllAsync kết thúc => stream đóng
                    Console.WriteLine($"[WARN] Stream bị đóng từ {node}");
                }
                catch (RpcException ex)
                {
                    Console.WriteLine($"[ERROR] Mất kết nối tới {node}: {ex.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Lỗi khác: {ex.Message}");
                }

                Console.WriteLine("[INFO] Thử kết nối lại sau 1 giây...");
                await Task.Delay(1000);
            }
        }

    }
}

