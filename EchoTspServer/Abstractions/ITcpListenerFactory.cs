using System.Net;

namespace EchoServer.Abstractions
{
    public interface ITcpListenerFactory
    {
        ITcpListenerWrapper Create(IPAddress address, int port);
    }
}
