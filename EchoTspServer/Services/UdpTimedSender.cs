using System;
using System.Net.Sockets;
using System.Threading;
using EchoServer.Abstractions;

namespace EchoServer.Services
{
    public class UdpTimedSender : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _host;
        private readonly int _port;
        private UdpClient? _udpClient;
        private Timer? _timer;
        private bool _isRunning;
        private bool _disposed;

        public UdpTimedSender(string host, int port, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _host = host;
            _port = port;
            _logger = logger;
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMs)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_isRunning)
                throw new InvalidOperationException("Sender is already running.");

            _isRunning = true;
            var random = new Random();

            _timer = new Timer(state =>
            {
                if (_disposed) return;

                try
                {
                    byte[] data = new byte[100];
                    random.NextBytes(data);
                    _udpClient?.Send(data, data.Length, _host, _port);
                    _logger.Log($"UDP data sent to {_host}:{_port}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error sending UDP data: {ex.Message}");
                }
            }, null, 0, intervalMs);
        }

        public void StopSending()
        {
            if (_disposed)
                return;

            if (!_isRunning)
                return;

            _timer?.Dispose();
            _timer = null;
            _isRunning = false;
            _logger.Log("UDP sender stopped.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    if (_isRunning)
                    {
                        _timer?.Dispose();
                        _isRunning = false;
                    }
                }
                catch
                {
                    // Ігноруємо помилки при зупинці
                }

                _udpClient?.Dispose();
                _timer?.Dispose();
            }

            _udpClient = null;
            _timer = null;
            _disposed = true;
        }
    }
}
