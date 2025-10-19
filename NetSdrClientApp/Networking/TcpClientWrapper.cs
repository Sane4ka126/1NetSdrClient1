using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public class TcpClientWrapper : ITcpClient
    {
        private string _host;
        private int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            _tcpClient = new TcpClient();
            try
            {
                _cts = new CancellationTokenSource();
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                _ = StartListeningAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (Connected)
            {
                _cts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
                _cts = null;
                _tcpClient = null;
                _stream = null;
                Console.WriteLine("Disconnected.");
            }
            else
            {
                Console.WriteLine("No active connection to disconnect.");
            }
        }

        public async Task SendMessageAsync(byte[] data)
        {
            await SendBytesAsync(data);
        }

        public async Task SendMessageAsync(string str)
        {
            var data = Encoding.UTF8.GetBytes(str);
            await SendBytesAsync(data);
        }

        private async Task SendBytesAsync(byte[] data)
        {
            ValidateConnection();
            LogSentMessage(data);
            await _stream!.WriteAsync(data.AsMemory(0, data.Length));
        }

        private void ValidateConnection()
        {
            if (!Connected || _stream == null || !_stream.CanWrite)
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        private static void LogSentMessage(byte[] data)
        {
            var hexString = string.Join(" ", data.Select(b => Convert.ToString(b, 16)));
            Console.WriteLine($"Message sent: {hexString}");
        }

        private async Task StartListeningAsync()
        {
            if (!Connected || _stream == null || !_stream.CanRead || _cts == null)
            {
                throw new InvalidOperationException("Not connected to a server.");
            }

            try
            {
                Console.WriteLine($"Starting listening for incomming messages.");
                while (!_cts.Token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[8194];
                    int bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token);
                    if (bytesRead > 0)
                    {
                        MessageReceived?.Invoke(this, buffer.AsSpan(0, bytesRead).ToArray());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listening loop: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
            }
        }
    }
}
