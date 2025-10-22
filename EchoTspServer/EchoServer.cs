using System;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Interfaces;

namespace EchoServer
{
    public class EchoServer
    {
        private readonly int _port;
        private readonly ITcpListenerFactory _listenerFactory;
        private readonly ILogger _logger;
        private ITcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;

        public int ClientsHandled { get; private set; }
        public bool IsRunning { get; private set; }

        public EchoServer(int port, ITcpListenerFactory listenerFactory, ILogger logger)
        {
            _port = port;
            _listenerFactory = listenerFactory ?? throw new ArgumentNullException(nameof(listenerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = _listenerFactory.CreateListener(_port);
            _listener.Start();
            IsRunning = true;
            _logger.Log($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    ITcpClient client = await _listener.AcceptTcpClientAsync();
                    _logger.Log("Client connected.");
                    ClientsHandled++;

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

            IsRunning = false;
            _logger.Log("Server shutdown.");
        }

        public virtual async Task HandleClientAsync(ITcpClient client, CancellationToken token)
        {
            try
            {
                INetworkStream stream = client.GetStream();
                await ProcessEchoStreamAsync(stream, token);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError($"Error handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
                _logger.Log("Client disconnected.");
            }
        }

        public virtual async Task ProcessEchoStreamAsync(INetworkStream stream, CancellationToken token)
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

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            _cancellationTokenSource.Dispose();
            IsRunning = false;
            _logger.Log("Server stopped.");
        }
    }
}
