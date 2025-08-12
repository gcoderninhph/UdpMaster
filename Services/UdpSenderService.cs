using System.Net.Sockets;

namespace udp.master
{
    public class UdpSenderService : IUdpSenderService
    {
        // Udp
        private UdpClient _udpClient;

        public UdpSenderService()
        {
            _udpClient = new UdpClient();
        }
        /// <summary>
        /// Gửi dữ liệu UDP đến địa chỉ và cổng chỉ định.
        /// </summary>
        /// <param name="address">Địa chỉ dạng "ip:port", ví dụ "127.0.0.1:5000"</param>
        /// <param name="data">Mảng byte cần gửi</param>
        public void Send(string address, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Address không được rỗng");

            var parts = address.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Address phải ở dạng ip:port");

            string ip = parts[0];
            if (!int.TryParse(parts[1], out int port))
                throw new ArgumentException("Port không hợp lệ");

            _udpClient.Send(data, data.Length, ip, port);
        }

        public void Send(string[] address, byte[] data)
        {
            foreach (var addr in address)
            {
                Send(addr, data);
            }
        }
    }
}