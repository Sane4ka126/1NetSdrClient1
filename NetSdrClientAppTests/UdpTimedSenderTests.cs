using System;
using System.Net;
using System.Threading;
using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace EchoServer.Tests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private Mock<ILogger> _loggerMock;
        private Mock<IUdpClient> _udpClientMock;
        private Mock<ITimer> _timerMock;
        private UdpTimedSender _sender;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger>();
            _udpClientMock = new Mock<IUdpClient>();
            _timerMock = new Mock<ITimer>();
            _sender = new UdpTimedSender("127.0.0.1", 60000, _loggerMock.Object, _udpClientMock.Object);
        }

        [Test]
        public void StartSending_InitializesTimer()
        {
            // Act
            _sender.StartSending(1000);

            // Assert
            _timerMock.Verify(t => t.Change(0, 1000), Times.Once());
        }

        [Test]
        public void StartSending_ThrowsIfAlreadyRunning()
        {
            // Arrange
            _sender.StartSending(1000);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _sender.StartSending(1000));
        }

        [Test]
        public void StopSending_DisposesTimer()
        {
            // Arrange
            _sender.StartSending(1000);

            // Act
            _sender.StopSending();

            // Assert
            _timerMock.Verify(t => t.Dispose(), Times.Once());
        }

        [Test]
        public void SendMessageCallback_SendsData()
        {
            // Arrange
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 60000);
            _udpClientMock.Setup(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()));

            // Act
            _sender.GetType()
                   .GetMethod("SendMessageCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                   .Invoke(_sender, new object[] { null });

            // Assert
            _udpClientMock.Verify(u => u.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.Is<IPEndPoint>(ep => ep.Address.ToString() == "127.0.0.1" && ep.Port == 60000)), Times.Once());
            _loggerMock.Verify(l => l.LogInfo("Message sent to 127.0.0.1:60000"), Times.Once());
        }

        [Test]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            _sender.StartSending(1000);

            // Act
            _sender.Dispose();

            // Assert
            _timerMock.Verify(t => t.Dispose(), Times.Once());
            _udpClientMock.Verify(u => u.Dispose(), Times.Once());
        }
    }
}
