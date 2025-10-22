using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogError(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void LogInfo(string message) => Console.WriteLine(message);
        public void LogError(string message) => Console.WriteLine($"Error: {message}");
    }

    public class NullLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogError(string message) { }
    }

    public interface ITcpListener : IDisposable
    {
        void Start();
        Task<TcpClient> AcceptTcpClientAsync();
        void Stop();
        IPEndPoint LocalEndpoint { get; }
    }

    public class TcpListenerWrapper : ITcpListener
    {
        private readonly TcpListener _listener;
        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }
        public void Start() => _listener.Start();
        public Task<TcpClient> AcceptTcpClientAsync() => _listener.AcceptTcpClientAsync();
        public void Stop() => _listener.Stop();
        public IPEndPoint LocalEndpoint => (IPEndPoint)_listener.LocalEndpoint;
        public void Dispose() => _listener.Stop();
    }

    public interface ITcpClient : IDisposable
    {
        NetworkStream GetStream();
    }

    public class TcpClientWrapper : ITcpClient
    {
        private readonly TcpClient _client;
        public TcpClientWrapper(TcpClient client) => _client = client;
        public NetworkStream GetStream() => _client.GetStream();
        public void Dispose() => _client.Dispose();
    }

    public class EchoServer
    {
        private readonly int _port;
        private readonly ITcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private Task _serverTask;
        private int _actualPort;
        public int ActualPort => _actualPort;
        public bool IsRunning => _serverTask != null && !_serverTask.IsCompleted;
        public int ActiveConnections { get; private set; }

        public EchoServer(int port, ILogger logger = null, ITcpListener listener = null)
        {
            _port = port;
            _logger = logger ?? new NullLogger();
            _listener = listener ?? new TcpListenerWrapper(IPAddress.Any, port);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener.Start();
            _actualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _logger.LogInfo($"Server started on port {_actualPort}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _logger.LogInfo("Client connected.");
                    Interlocked.Increment(ref ActiveConnections);

                    _ = Task.Run(() => HandleClientAsync(new TcpClientWrapper(client), _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _logger.LogError($"Accept error: {ex.Message}");
                    }
                    break;
                }
            }

            _logger.LogInfo("Server shutdown.");
        }

        public void Start()
        {
            if (_serverTask != null)
                throw new InvalidOperationException("Server is already running.");

            _serverTask = Task.Run(() => StartAsync());
        }

        private async Task HandleClientAsync(ITcpClient client, CancellationToken token)
        {
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while (!token.IsCancellationRequested &&
                           (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead, token);
                        _logger.LogInfo($"Echoed {bytesRead} bytes to the client.");
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _logger.LogError($"Client error: {ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref ActiveConnections);
                    client.Dispose();
                    _logger.LogInfo("Client disconnected.");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            _logger.LogInfo("Server stopped.");
        }

        public async Task StopAsync(TimeSpan timeout = default)
        {
            if (timeout == default)
                timeout = TimeSpan.FromSeconds(5);

            Stop();

            if (_serverTask != null)
            {
                var completed = await Task.WhenAny(_serverTask, Task.Delay(timeout));
                if (completed != _serverTask)
                {
                    _logger.LogError("Server did not stop gracefully within timeout.");
                }
            }

            _cancellationTokenSource?.Dispose();
        }
    }

    public class ServerApplication
    {
        public static async Task RunAsync(string[] args)
        {
            var logger = new ConsoleLogger();
            EchoServer server = new EchoServer(5000, logger);
            server.Start();
            await Task.Delay(500);

            // Залишено місце для UdpTimedSender, якщо він буде доданий
            logger.LogInfo("Press 'q' to quit...");
            while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
            {
            }

            await server.StopAsync();
            logger.LogInfo("Application stopped.");
        }
    }

    class Program
    {
        static Task Main(string[] args) => ServerApplication.RunAsync(args);
    }
}
