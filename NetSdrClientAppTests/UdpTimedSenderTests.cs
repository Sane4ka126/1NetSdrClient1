using System;
using System.Net;
using System.Threading;
using EchoServer;
using EchoServer.Interfaces;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private Mock<IUdpClient> _mockUdpClient;
        private Mock<IMessageGenerator> _mockMessageGenerator;
        private Mock<ILogger> _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockUdpClient = new Mock<IUdpClient>();
            _mockMessageGenerator = new Mock<IMessageGenerator>();
            _mockLogger = new Mock<ILogger>();
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenHostIsNull()
        {
            Action act = () => new UdpTimedSender(null, 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("host");
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenUdpClientIsNull()
        {
            Action act = () => new UdpTimedSender("127.0.0.1", 5000, null, _mockMessageGenerator.Object, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("udpClient");
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenMessageGeneratorIsNull()
        {
            Action act = () => new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, null, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("messageGenerator");
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            Action act = () => new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Test]
        public void StartSending_ShouldThrowInvalidOperationException_WhenAlreadyRunning()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            sender.StartSending(1000);

            Action act = () => sender.StartSending(1000);
            act.Should().Throw<InvalidOperationException>().WithMessage("Sender is already running.");

            sender.Dispose();
        }

        [Test]
        public void StartSending_ShouldSendMessages()
        {
            var expectedMessage = new byte[] { 1, 2, 3 };
            _mockMessageGenerator.Setup(g => g.GenerateMessage(It.IsAny<ushort>())).Returns(expectedMessage);

            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            sender.StartSending(100);

            Thread.Sleep(250);
            sender.StopSending();

            _mockUdpClient.Verify(c => c.Send(
                It.IsAny<byte[]>(), 
                It.IsAny<int>(), 
                It.IsAny<IPEndPoint>()), 
                Times.AtLeast(2));

            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Message sent"))), Times.AtLeast(2));
        }

        [Test]
        public void StartSending_ShouldIncrementSequenceNumber()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            var initialSequence = sender.CurrentSequenceNumber;

            sender.StartSending(100);
            Thread.Sleep(250);
            sender.StopSending();

            sender.CurrentSequenceNumber.Should().BeGreaterThan(initialSequence);
        }

        [Test]
        public void StopSending_ShouldStopSendingMessages()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            sender.StartSending(100);
            Thread.Sleep(150);
            
            _mockUdpClient.Invocations.Clear();

            sender.StopSending();
            Thread.Sleep(200);

            _mockUdpClient.Verify(c => c.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IPEndPoint>()), Times.Never);
        }

        [Test]
        public void IsRunning_ShouldReturnTrue_WhenSenderIsRunning()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            
            sender.IsRunning.Should().BeFalse();
            
            sender.StartSending(1000);
            sender.IsRunning.Should().BeTrue();
            
            sender.StopSending();
            sender.IsRunning.Should().BeFalse();
        }

        [Test]
        public void Dispose_ShouldStopSending()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            sender.StartSending(1000);
            
            sender.Dispose();
            
            sender.IsRunning.Should().BeFalse();
            _mockUdpClient.Verify(c => c.Dispose(), Times.Once);
        }

        [Test]
        public void CurrentSequenceNumber_ShouldBeZero_Initially()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockUdpClient.Object, _mockMessageGenerator.Object, _mockLogger.Object);
            sender.CurrentSequenceNumber.Should().Be(0);
        }
    }
}
