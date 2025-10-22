using System;
using System.Net;
using System.Net.Sockets;
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

    public interface ITcpClient : IDisposable
    {
        NetworkStream GetStream();
    }

    public interface IUdpClient : IDisposable
    {
        void Send(byte[] dgram, int bytes, IPEndPoint endPoint);
    }

    public interface ITimer : IDisposable
    {
        void Change(int dueTime, int period);
    }
}
