using System;
using System.Net;

namespace EchoServer.Interfaces
{
    public interface IUdpClient : IDisposable
    {
        void Send(byte[] data, int length, IPEndPoint endpoint);
    }

    public interface IMessageGenerator
    {
        byte[] GenerateMessage(ushort sequenceNumber);
    }
}
