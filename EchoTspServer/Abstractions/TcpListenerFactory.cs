using System.Net;

namespace EchoServer.Abstractions
{
    public class TcpListenerFactory : ITcpListenerFactory
    {
        public ITcpListenerWrapper Create(IPAddress address, int port)
        {
            return new TcpListenerWrapper(address, port);
        }
    }
}
