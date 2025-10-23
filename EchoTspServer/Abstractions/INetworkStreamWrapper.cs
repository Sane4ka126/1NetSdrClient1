using System;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer.Abstractions
{
    public interface INetworkStreamWrapper : IDisposable
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
    }
}
