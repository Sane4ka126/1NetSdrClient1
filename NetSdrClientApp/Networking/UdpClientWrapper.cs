using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking;

public class TcpClientWrapper : ITcpClient
{
    private readonly string _host;  // ✅ Зробили readonly
    private readonly int _port;     // ✅ Зробили readonly
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;  // ✅ Nullable

    public event EventHandler<byte[]>? MessageReceived;

    public bool Connected => _tcpClient?.Connected ?? false;

    public TcpClientWrapper(string host, int port)
    {
        _host = host;
        _port = port;
        // ✅ _cts не ініціалізується в конструкторі, оскільки він nullable
    }

    public void Connect()
    {
        try
        {
            _tcpClient = new TcpClient(_host, _port);
            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();

            Task.Run(() => ListenForMessagesAsync());

            Console.WriteLine($"Connected to {_host}:{_port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();  // ✅ Dispose CancellationTokenSource
            _stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine("Disconnected from server.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while disconnecting: {ex.Message}");
        }
        finally
        {
            _cts = null;  // ✅ Присвоєння null через nullable
        }
    }

    public async Task SendMessageAsync(byte[] message)
    {
        if (_stream == null || !_stream.CanWrite)
        {
            Console.WriteLine("Cannot send message. Stream is not available.");
            return;
        }

        try
        {
            // ✅ Використовуємо ReadOnlyMemory<byte> overload
            await _stream.WriteAsync(new ReadOnlyMemory<byte>(message), _cts?.Token ?? CancellationToken.None);
            await _stream.FlushAsync(_cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (_stream == null || !_stream.CanWrite)
        {
            Console.WriteLine("Cannot send message. Stream is not available.");
            return;
        }

        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            // ✅ Використовуємо ReadOnlyMemory<byte> overload
            await _stream.WriteAsync(new ReadOnlyMemory<byte>(data), _cts?.Token ?? CancellationToken.None);
            await _stream.FlushAsync(_cts?.Token ?? CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    private async Task ListenForMessagesAsync()
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (_stream != null && _stream.CanRead && !(_cts?.Token.IsCancellationRequested ?? true))
            {
                // ✅ Використовуємо Memory<byte> overload
                int bytesRead = await _stream.ReadAsync(new Memory<byte>(buffer), _cts?.Token ?? CancellationToken.None);

                if (bytesRead > 0)
                {
                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);
                    MessageReceived?.Invoke(this, receivedData);
                }
                else
                {
                    Console.WriteLine("Connection closed by server.");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Listening cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving message: {ex.Message}");
        }
    }
}
