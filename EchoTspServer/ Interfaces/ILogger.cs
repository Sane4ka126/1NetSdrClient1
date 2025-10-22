using System;

namespace EchoServer.Interfaces
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine(message);
        public void LogError(string message) => Console.Error.WriteLine(message);
    }
}
