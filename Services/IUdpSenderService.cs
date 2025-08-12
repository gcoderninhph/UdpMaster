namespace udp.master
{
    public interface IUdpSenderService
    {
        void Send(string address, byte[] data);
        void Send(string[] address, byte[] data);
    }
}