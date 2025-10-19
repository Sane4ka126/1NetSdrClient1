using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking;

public class UdpClientWrapper : IUdpClient
{
    private readonly IPEndPoint _localEndPoint;
    private CancellationTokenSource? _cts;
    private UdpClient? _udpClient;

    public event EventHandler<byte[]>? MessageReceived;

    public UdpClientWrapper(int port)
    {
        _localEndPoint = new IPEndPoint(IPAddress.Any, port);
    }

    public async Task StartListeningAsync()
    {
        _cts = new CancellationTokenSource();
        try
        {
            _udpClient = new UdpClient(_localEndPoint);
            while (!_cts.Token.IsCancellationRequested)
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync(_cts.Token);
                MessageReceived?.Invoke(this, result.Buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, no action needed
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Socket error: {ex.Message}");
            throw; // Re-throw для можливості обробки в тестах
        }
        catch (ObjectDisposedException)
        {
            // Client was disposed, expected during shutdown
        }
    }

    public void StopListening()
    {
        CleanupResources();
    }

    public void Exit()
    {
        CleanupResources();
    }

    private void CleanupResources()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping: {ex.Message}");
        }
    }

    // ✅ Виправлено: порівнюємо тільки порти, бо IPAddress.Any може бути різними інстансами
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        var other = (UdpClientWrapper)obj;
        return _localEndPoint.Port == other._localEndPoint.Port;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            nameof(UdpClientWrapper), 
            _localEndPoint.Port
        );
    }
}
