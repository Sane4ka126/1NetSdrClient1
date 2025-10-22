using System.Threading;
using System.Threading.Tasks;

namespace EchoServer.Interfaces
{
    public interface INetworkStream
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
    }

    public interface ITcpClient
    {
        INetworkStream GetStream();
        void Close();
    }

    public interface ITcpListener
    {
        void Start();
        void Stop();
        Task<ITcpClient> AcceptTcpClientAsync();
    }

    public interface ITcpListenerFactory
    {
        ITcpListener CreateListener(int port);
    }
}
