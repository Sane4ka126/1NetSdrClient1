using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
        private ushort _counter = 0;
        
        // S2245: Random is used only for generating test data payload, not for any security-sensitive operations
#pragma warning disable S2245
        private readonly Random _random = new Random();
#pragma warning restore S2245

        public UdpTimedSender(string host, int port, ILogger logger)
        {
            _host = host;
            _port = port;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");
            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object? state)
        {
            try
            {
                byte[] samples = new byte[1024];
                _random.NextBytes(samples);
                _counter++;

                byte[] msg = (new byte[] { 0x04, 0x84 })
                    .Concat(BitConverter.GetBytes(_counter))
                    .Concat(samples)
                    .ToArray();
                    
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _udpClient.Send(msg, msg.Length, endpoint);
                _logger.Log($"Message sent to {_host}:{_port}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            StopSending();
            _udpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
