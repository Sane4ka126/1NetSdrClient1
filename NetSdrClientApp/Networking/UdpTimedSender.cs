using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetSdrClientApp.Networking
{
    public interface IUdpClient : IDisposable
    {
        void Send(byte[] dgram, int bytes, IPEndPoint endPoint);
    }

    public class UdpClientWrapper : IUdpClient
    {
        private readonly UdpClient _udpClient;
        public UdpClientWrapper() => _udpClient = new UdpClient();
        public void Send(byte[] dgram, int bytes, IPEndPoint endPoint) => _udpClient.Send(dgram, bytes, endPoint);
        public void Dispose() => _udpClient.Dispose();
    }

    public interface ITimer : IDisposable
    {
        void Change(int dueTime, int period);
    }

    public class TimerWrapper : ITimer
    {
        private readonly Timer _timer;
        public TimerWrapper(TimerCallback callback, object state, int dueTime, int period)
        {
            _timer = new Timer(callback, state, dueTime, period);
        }
        public void Change(int dueTime, int period) => _timer.Change(dueTime, period);
        public void Dispose() => _timer.Dispose();
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IUdpClient _udpClient;
        private readonly ILogger _logger;
        private ITimer _timer;
        private ushort _counter;

        public UdpTimedSender(string host, int port, ILogger logger = null, IUdpClient udpClient = null)
        {
            _host = host;
            _port = port;
            _udpClient = udpClient ?? new UdpClientWrapper();
            _logger = logger ?? new NullLogger();
            _counter = 0;
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new TimerWrapper(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        private void SendMessageCallback(object state)
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
                _logger.LogInfo($"Message sent to {_host}:{_port}");
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
            _udpClient?.Dispose();
        }
    }
}
