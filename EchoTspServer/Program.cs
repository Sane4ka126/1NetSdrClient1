using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    // Абстракція для TcpListener
    public interface ITcpListenerWrapper
    {
        void Start();
        void Stop();
        Task<ITcpClientWrapper> AcceptTcpClientAsync();
    }

    // Абстракція для TcpClient
    public interface ITcpClientWrapper : IDisposable
    {
        INetworkStreamWrapper GetStream();
        void Close();
    }

    // Абстракція для NetworkStream
    public interface INetworkStreamWrapper : IDisposable
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);
    }

    // Реальні реалізації для production
    public class TcpListenerWrapper : ITcpListenerWrapper
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start() => _listener.Start();
        public void Stop() => _listener.Stop();

        public async Task<ITcpClientWrapper> AcceptTcpClientAsync()
        {
            var client = await _listener.AcceptTcpClientAsync();
            return new TcpClientWrapper(client);
        }
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

        public void Close() => _client.Close();
        public void Dispose() => _client.Dispose();
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

        public void Dispose() => _stream.Dispose();
    }

    // Рефакторений EchoServer з інжекцією залежностей
    public class EchoServer
    {
        private readonly int _port;
        private readonly Func<IPAddress, int, ITcpListenerWrapper> _listenerFactory;
        private readonly ILogger _logger;
        private ITcpListenerWrapper _listener;
        private CancellationTokenSource _cancellationTokenSource;

        public EchoServer(int port, Func<IPAddress, int, ITcpListenerWrapper> listenerFactory = null, ILogger logger = null)
        {
            _port = port;
            _listenerFactory = listenerFactory ?? ((addr, p) => new TcpListenerWrapper(addr, p));
            _logger = logger ?? new ConsoleLogger();
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
                    _logger.Log($"Error: {ex.Message}");
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

    // Інтерфейс для логування
    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine(message);
    }

    // UdpTimedSender з абстракціями
    public interface IUdpClientWrapper : IDisposable
    {
        int Send(byte[] data, int length, IPEndPoint endpoint);
    }

    public class UdpClientWrapper : IUdpClientWrapper
    {
        private readonly UdpClient _client;

        public UdpClientWrapper()
        {
            _client = new UdpClient();
        }

        public int Send(byte[] data, int length, IPEndPoint endpoint)
        {
            return _client.Send(data, length, endpoint);
        }

        public void Dispose() => _client.Dispose();
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IUdpClientWrapper _udpClient;
        private readonly ILogger _logger;
        private Timer _timer;
        private ushort _counter = 0;

        public UdpTimedSender(string host, int port, IUdpClientWrapper udpClient = null, ILogger logger = null)
        {
            _host = host;
            _port = port;
            _udpClient = udpClient ?? new UdpClientWrapper();
            _logger = logger ?? new ConsoleLogger();
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
                byte[] msg = CreateMessage();
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                _logger.Log($"Message sent to {_host}:{_port}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error sending message: {ex.Message}");
            }
        }

        internal byte[] CreateMessage()
        {
            Random rnd = new Random();
            byte[] samples = new byte[1024];
            rnd.NextBytes(samples);
            _counter++;

            byte[] header = new byte[] { 0x04, 0x84 };
            byte[] counterBytes = BitConverter.GetBytes(_counter);
            
            byte[] msg = new byte[header.Length + counterBytes.Length + samples.Length];
            Buffer.BlockCopy(header, 0, msg, 0, header.Length);
            Buffer.BlockCopy(counterBytes, 0, msg, header.Length, counterBytes.Length);
            Buffer.BlockCopy(samples, 0, msg, header.Length + counterBytes.Length, samples.Length);
            
            return msg;
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

    // Program class для запуску
    public class Program
    {
        public static async Task Main(string[] args)
        {
            EchoServer server = new EchoServer(5000);

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
                    // Wait until 'q' is pressed
                }

                sender.StopSending();
                server.Stop();
                Console.WriteLine("Sender stopped.");
            }
        }
    }
}
