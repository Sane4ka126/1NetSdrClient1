using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Interfaces;

namespace EchoServer.Wrappers
{
    public class NetworkStreamWrapper : INetworkStream
    {
        private readonly NetworkStream _stream;

        public NetworkStreamWrapper(NetworkStream stream)
        {
            _stream = stream;
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return _stream.ReadAsync(buffer, offset, count, token);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return _stream.WriteAsync(buffer, offset, count, token);
        }
    }

    public class TcpClientWrapper : ITcpClient
    {
        private readonly TcpClient _client;
        private INetworkStream _streamWrapper;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client;
        }

        public INetworkStream GetStream()
        {
            return _streamWrapper ??= new NetworkStreamWrapper(_client.GetStream());
        }

        public void Close()
        {
            _client.Close();
        }
    }

    public class TcpListenerWrapper : ITcpListener
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(TcpListener listener)
        {
            _listener = listener;
        }

        public void Start() => _listener.Start();
        public void Stop() => _listener.Stop();

        public async Task<ITcpClient> AcceptTcpClientAsync()
        {
            var client = await _listener.AcceptTcpClientAsync();
            return new TcpClientWrapper(client);
        }
    }

    public class TcpListenerFactory : ITcpListenerFactory
    {
        public ITcpListener CreateListener(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            return new TcpListenerWrapper(listener);
        }
    }
}
