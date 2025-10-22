using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    // Інтерфейс для покращення тестованості
    public interface INetworkStreamWrapper
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
        void Close();
    }

    public class NetworkStreamWrapper : INetworkStreamWrapper
    {
        private readonly NetworkStream _stream;

        public NetworkStreamWrapper(NetworkStream stream)
        {
            _stream = stream;
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return _stream.ReadAsync(buffer, offset, count, token);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            return _stream.WriteAsync(buffer, offset, count, token);
        }

        public void Close()
        {
            _stream.Close();
        }
    }

    public interface ITcpClientWrapper
    {
        INetworkStreamWrapper GetStream();
        void Close();
    }

    public class TcpClientWrapper : ITcpClientWrapper
    {
        private readonly TcpClient _client;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client;
        }

        public INetworkStreamWrapper GetStream()
        {
            return new NetworkStreamWrapper(_client.GetStream());
        }

        public void Close()
        {
            _client.Close();
        }
    }

    public interface ITcpListenerWrapper
    {
        void Start();
        void Stop();
        Task<ITcpClientWrapper> AcceptTcpClientAsync();
    }

    public class TcpListenerWrapper : ITcpListenerWrapper
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public async Task<ITcpClientWrapper> AcceptTcpClientAsync()
        {
            var client = await _listener.AcceptTcpClientAsync();
            return new TcpClientWrapper(client);
        }
    }

    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }

    public class EchoServer
    {
        private readonly int _port;
        private readonly ILogger _logger;
        private readonly Func<IPAddress, int, ITcpListenerWrapper> _listenerFactory;
        private ITcpListenerWrapper _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private const int BufferSize = 8192;

        // Конструктор з dependency injection
        public EchoServer(int port, ILogger logger = null, 
            Func<IPAddress, int, ITcpListenerWrapper> listenerFactory = null)
        {
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");

            _port = port;
            _logger = logger ?? new ConsoleLogger();
            _listenerFactory = listenerFactory ?? ((addr, p) => new TcpListenerWrapper(addr, p));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = _listenerFactory(IPAddress.Any, _port);
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
                    // Listener has been closed
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error accepting client: {ex.Message}");
                    break;
                }
            }

            _logger.Log("Server shutdown.");
        }

        internal async Task HandleClientAsync(ITcpClientWrapper client, CancellationToken token)
        {
            INetworkStreamWrapper stream = null;
            try
            {
                stream = client.GetStream();
                byte[] buffer = new byte[BufferSize];
                int bytesRead;

                while (!token.IsCancellationRequested && 
                       (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    // Echo back the received message
                    await stream.WriteAsync(buffer, 0, bytesRead, token);
                    _logger.Log($"Echoed {bytesRead} bytes to the client.");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.Log($"Error: {ex.Message}");
            }
            finally
            {
                stream?.Close();
                client.Close();
                _logger.Log("Client disconnected.");
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            _cancellationTokenSource.Dispose();
            _logger.Log("Server stopped.");
        }

        public static async Task Main(string[] args)
        {
            EchoServer server = new EchoServer(5000);

            // Start the server in a separate task
            _ = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1";
            int port = 60000;
            int intervalMilliseconds = 5000;

            using (var sender = new UdpTimedSender(host, port))
            {
                Console.WriteLine("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                Console.WriteLine("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                    // Just wait until 'q' is pressed
                }

                sender.StopSending();
                server.Stop();
                Console.WriteLine("Sender stopped.");
            }
        }
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer _timer;
        private ushort _sequenceNumber = 0;

        public UdpTimedSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
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
                Random rnd = new Random();
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
                _sequenceNumber++;

                byte[] msg = new byte[] { 0x04, 0x84 }
                    .Concat(BitConverter.GetBytes(_sequenceNumber))
                    .Concat(samples)
                    .ToArray();
                
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
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
