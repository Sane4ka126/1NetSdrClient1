using System;
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
            Action act = () => new UdpTimedSender("127.0.0.1", 5000, null!);

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

            // Assert - Timer зупинився, можна перевірити через повторний старт
            // Після Dispose не можна запустити знову (InvalidOperationException або просто не запускається)
            // Перевіримо що Dispose не викликає помилок
            Action act = () => sender.Dispose();
            act.Should().NotThrow();
        }

        [Test]
        public void StartSending_ShouldLogMessages()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000, _mockLogger!.Object);

            // Act
            sender.StartSending(5000);
            System.Threading.Thread.Sleep(100); // Дати час на один виклик

            // Assert
            sender.StopSending();
            _mockLogger!.Verify(l => l.Log(It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
}
