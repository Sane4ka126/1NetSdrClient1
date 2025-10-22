using System.Net;
using System.Net.Sockets;
using EchoServer.Wrappers;
using FluentAssertions;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class WrapperTests
    {
        [Test]
        public void TcpListenerFactory_ShouldCreateListener()
        {
            var factory = new TcpListenerFactory();
            
            var listener = factory.CreateListener(5001);

            listener.Should().NotBeNull();
        }

        [Test]
        public void UdpClientWrapper_ShouldBeDisposable()
        {
            var wrapper = new UdpClientWrapper();

            Action act = () => wrapper.Dispose();

            act.Should().NotThrow();
        }

        [Test]
        public void UdpClientWrapper_ShouldSendData()
        {
            using var wrapper = new UdpClientWrapper();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data = new byte[] { 1, 2, 3 };

            Action act = () => wrapper.Send(data, data.Length, endpoint);

            act.Should().NotThrow();
        }
    }
}
