using System;
using System.Threading.Tasks;
using EchoServer.Abstractions;
using EchoServer.Services;

namespace EchoServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ILogger logger = new ConsoleLogger();
            ITcpListenerFactory listenerFactory = new TcpListenerFactory();

            EchoServerService server = new EchoServerService(5000, logger, listenerFactory);

            _ = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1";
            int port = 60000;
            int intervalMilliseconds = 5000;

            using (var sender = new UdpTimedSender(host, port, logger))
            {
                logger.Log("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                logger.Log("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                }

                sender.StopSending();
                server.Stop();
                logger.Log("Sender stopped.");
            }
        }
    }
}
