using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EchoServer.Abstractions;

namespace EchoServer.Services
{
    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly ILogger _logger;
        private readonly UdpClient _udpClient;
        private Timer? _timer;
        private int _counter;
        private bool _isRunning;
        private readonly object _lock = new object();

        public UdpTimedSender(string host, int port, ILogger logger)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _udpClient = new UdpClient();
            _counter = 0;
            _isRunning = false;
        }

        public void StartSending(int intervalMs)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    throw new InvalidOperationException("Sender is already running.");
                }

                _isRunning = true;
                _timer = new Timer(SendMessageCallback, null, 0, intervalMs);
                _logger.Log($"Started sending messages to {_host}:{_port} every {intervalMs}ms");
            }
        }

        public void StopSending()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    _timer.Dispose();
                    _timer = null;
                }

                _isRunning = false;
                _logger.Log("Stopped sending messages");
            }
        }

        private void SendMessageCallback(object? state)
        {
            try
            {
                // S2245: Random used for generating test data payload, not for security purposes
                #pragma warning disable S2245
                Random rnd = new Random();
                #pragma warning restore S2245
                
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
                _counter++;

                byte[] msg = (new byte[] { 0x04, 0x84 })
                    .Concat(BitConverter.GetBytes(_counter))
                    .Concat(samples)
                    .ToArray();

                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _udpClient.Send(msg, msg.Length, endPoint);

                _logger.Log($"Message sent to {_host}:{_port} - Counter: {_counter}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error sending message: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopSending();
            _udpClient?.Dispose();
        }
    }

    // Extension method for byte array concatenation
    public static class ByteArrayExtensions
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
