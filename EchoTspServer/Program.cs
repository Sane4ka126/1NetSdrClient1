using System;
using System.Threading.Tasks;
using EchoServer.Interfaces;
using EchoServer.Wrappers;

namespace EchoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var logger = new ConsoleLogger();
            var listenerFactory = new TcpListenerFactory();
            var server = new EchoServer(5000, listenerFactory, logger);

            _ = Task.Run(() => server.StartAsync());

            string host = "127.0.0.1";
            int port = 60000;
            int intervalMilliseconds = 5000;

            using (var udpClient = new UdpClientWrapper())
            using (var sender = new UdpTimedSender(
                host, 
                port, 
                udpClient, 
                new RandomMessageGenerator(), 
                logger))
            {
                Console.WriteLine("Press any key to stop sending...");
                sender.StartSending(intervalMilliseconds);

                Console.WriteLine("Press 'q' to quit...");
                while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q)
                {
                }

                sender.StopSending();
                server.Stop();
                Console.WriteLine("Application stopped.");
            }
        }
    }
}
