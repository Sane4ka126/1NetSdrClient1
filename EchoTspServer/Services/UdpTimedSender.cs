using System;
using System.Linq;
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
        private ushort _counter = 0;

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
                Random rnd = new Random();
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
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
