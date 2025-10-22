using System;
using System.Threading.Tasks;

namespace EchoServer.Abstractions
{
    public interface ITcpListenerWrapper : IDisposable
    {
        void Start();
        void Stop();
        Task<ITcpClientWrapper> AcceptTcpClientAsync();
    }
}
