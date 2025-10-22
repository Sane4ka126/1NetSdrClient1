using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using EchoServer.Interfaces;

namespace EchoServer.Wrappers
{
    public class UdpClientWrapper : IUdpClient
    {
        private readonly UdpClient _client;

        public UdpClientWrapper()
        {
            _client = new UdpClient();
        }

        public void Send(byte[] data, int length, IPEndPoint endpoint)
        {
            _client.Send(data, length, endpoint);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }

    public class RandomMessageGenerator : IMessageGenerator
    {
        private readonly Random _random;

        public RandomMessageGenerator()
        {
            _random = new Random();
        }

        public byte[] GenerateMessage(ushort sequenceNumber)
        {
            byte[] samples = new byte[1024];
            _random.NextBytes(samples);

            return new byte[] { 0x04, 0x84 }
                .Concat(BitConverter.GetBytes(sequenceNumber))
                .Concat(samples)
                .ToArray();
        }
    }
}
