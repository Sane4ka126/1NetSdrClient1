using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Abstractions;

namespace EchoServer.Services
{
    public class EchoServerService
    {
        private readonly int _port;
        private readonly ILogger _logger;
        private readonly ITcpListenerFactory _listenerFactory;
        private ITcpListenerWrapper? _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public EchoServerService(int port, ILogger logger, ITcpListenerFactory listenerFactory)
        {
            _port = port;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _listenerFactory = listenerFactory ?? throw new ArgumentNullException(nameof(listenerFactory));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = _listenerFactory.Create(IPAddress.Any, _port);
            _listener.Start();
            _logger.Log($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    ITcpClientWrapper client = await _listener.AcceptTcpClientAsync();
                    _logger.Log("Client connected.");

                    _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error accepting client: {ex.Message}");
                    break;
                }
            }

            _logger.Log("Server shutdown.");
        }

        public async Task HandleClientAsync(ITcpClientWrapper client, CancellationToken token)
        {
            using (INetworkStreamWrapper stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while (!token.IsCancellationRequested && 
                           (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead, token);
                        _logger.Log($"Echoed {bytesRead} bytes to the client.");
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError($"Error: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    _logger.Log("Client disconnected.");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            _cancellationTokenSource.Dispose();
            _logger.Log("Server stopped.");
        }
    }
}
