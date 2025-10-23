using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoServer.Abstractions
{
    public class TcpListenerWrapper : ITcpListenerWrapper
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<ITcpClientWrapper> AcceptTcpClientAsync()
        {
            var client = await _listener.AcceptTcpClientAsync();
            return new TcpClientWrapper(client);
        }

        public void Dispose()
        {
            _listener?.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
