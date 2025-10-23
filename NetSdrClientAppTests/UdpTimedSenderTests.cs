using System;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Abstractions;
using EchoServer.Services;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private Mock<ILogger>? _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Test]
        public void Constructor_ShouldThrowException_WhenLoggerIsNull()
        {
            // Arrange & Act
            Action act = () => _ = new UdpTimedSender("127.0.0.1", 5000, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public void StartSending_ShouldThrowException_WhenAlreadyRunning()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 5000, _mockLogger!.Object);
            sender.StartSending(1000);

            // Act
            Action act = () => sender.StartSending(1000);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Sender is already running.");
            
            sender.StopSending();
        }

        [Test]
        public void StopSending_ShouldNotThrow_WhenNotStarted()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 5000, _mockLogger!.Object);

            // Act
            Action act = () => sender.StopSending();

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Dispose_ShouldStopTimer()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockLogger!.Object);
            sender.StartSending(1000);

            // Act
            sender.Dispose();

            // Assert
            Action act = () => sender.Dispose();
            act.Should().NotThrow();
        }

        [Test]
        public async Task StartSending_ShouldLogMessages()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000, _mockLogger!.Object);

            // Act
            sender.StartSending(5000);
            await Task.Delay(100);

            // Assert
            sender.StopSending();
            _mockLogger!.Verify(l => l.Log(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task StartSending_ShouldSendMessagesWithIncrementingCounter()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60001, _mockLogger!.Object);

            // Act
            sender.StartSending(100);
            await Task.Delay(350); // Wait for ~3 sends
            sender.StopSending();

            // Assert
            _mockLogger!.Verify(
                l => l.Log(It.Is<string>(s => s.Contains("Message sent to 127.0.0.1:60001"))),
                Times.AtLeast(2));
        }

        [Test]
        public async Task StopSending_ShouldStopTimer()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60002, _mockLogger!.Object);
            sender.StartSending(100);

            // Act
            sender.StopSending();
            await Task.Delay(300);

            // Assert - no new messages after stop
            _mockLogger!.Invocations.Clear();
            await Task.Delay(200);
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Constructor_ShouldInitializeWithValidParameters()
        {
            // Arrange & Act
            Action act = () =>
            {
                using var sender = new UdpTimedSender("192.168.1.1", 8080, _mockLogger!.Object);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Dispose_MultipleCalls_ShouldNotThrow()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 5000, _mockLogger!.Object);

            // Act & Assert
            sender.Dispose();
            Action act = () => sender.Dispose();
            act.Should().NotThrow();
        }
    }
}
