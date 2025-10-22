using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace EchoServer
{
    // Інтерфейс для абстракції логування
    public interface ILogger
    {
        void LogInfo(string message);
        void LogError(string message);
    }

    // Консольний логер для продакшн-коду
    public class ConsoleLogger : ILogger
    {
        public void LogInfo(string message) => Console.WriteLine(message);
        public void LogError(string message) => Console.WriteLine($"Error: {message}");
    }

    // Тихий логер для тестів
    public class NullLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogError(string message) { }
    }

    public class EchoServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private Task _serverTask;
        private int _actualPort;

        // Властивості для тестування
        public int ActualPort => _actualPort;
        public bool IsRunning => _serverTask != null && !_serverTask.IsCompleted;
        public int ActiveConnections { get; private set; }

        // Конструктор з інверсією залежностей
        public EchoServer(int port, ILogger logger = null)
        {
            _port = port;
            _logger = logger ?? new NullLogger();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            
            // Отримуємо реальний порт (важливо для порту 0)
            _actualPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _logger.LogInfo($"Server started on port {_actualPort}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _logger.LogInfo("Client connected.");
                    Interlocked.Increment(ref ActiveConnections);

                    _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
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

        // Метод для неблокуючого запуску
        public void Start()
        {
            if (_serverTask != null)
                throw new InvalidOperationException("Server is already running.");

            _serverTask = Task.Run(() => StartAsync());
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
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
                    client.Close();
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

        // Метод для коректного очищення ресурсів
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

        public static async Task Main(string[] args)
        {
            var logger = new ConsoleLogger();
            EchoServer server = new EchoServer(5000, logger);

            // Запуск сервера
            server.Start();

            // Очікуємо, поки сервер запуститься
            await Task.Delay(500);

            string host = "127.0.0.1";
            int port = 60000;
            int intervalMilliseconds = 5000;

            using (var sender = new UdpTimedSender(host, port, logger))
            {
                logger.LogInfo("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                logger.LogInfo("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                    // Чекаємо натискання 'q'
                }

                sender.StopSending();
                await server.StopAsync();
                logger.LogInfo("Application stopped.");
            }
        }
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private readonly ILogger _logger;
        private Timer _timer;
        private ushort _counter;

        public UdpTimedSender(string host, int port, ILogger logger = null)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
            _logger = logger ?? new NullLogger();
            _counter = 0;
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
                // Генерація даних
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
