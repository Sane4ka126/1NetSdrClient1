using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking.Interfaces
{
    public interface INetworkStream : IDisposable
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
    }
}
