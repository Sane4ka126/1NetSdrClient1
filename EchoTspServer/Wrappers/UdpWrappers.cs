using System;
using System.Net;
using System.Threading;
using EchoServer.Interfaces;

namespace EchoServer
{
    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IUdpClient _udpClient;
        private readonly IMessageGenerator _messageGenerator;
        private readonly ILogger _logger;
        private Timer? _timer;
        private ushort _sequenceNumber = 0;

        public bool IsRunning => _timer != null;
        public ushort CurrentSequenceNumber => _sequenceNumber;

        public UdpTimedSender(
            string host, 
            int port, 
            IUdpClient udpClient, 
            IMessageGenerator messageGenerator,
            ILogger logger)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
            _messageGenerator = messageGenerator ?? throw new ArgumentNullException(nameof(messageGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object state)
        {
            try
            {
                _sequenceNumber++;
                byte[] msg = _messageGenerator.GenerateMessage(_sequenceNumber);
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
        }
    }
}
