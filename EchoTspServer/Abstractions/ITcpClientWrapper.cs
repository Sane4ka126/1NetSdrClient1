using System;

namespace EchoServer.Abstractions
{
    public interface ITcpClientWrapper : IDisposable
    {
        INetworkStreamWrapper GetStream();
        void Close();
    }
}
