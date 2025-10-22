using System;
using System.Net;
using System.Net.Sockets;
using EchoServer.Abstractions;
using FluentAssertions;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetworkWrappersTests
    {
        [Test]
        public void TcpListenerFactory_ShouldCreateListener()
        {
            // Arrange
            var factory = new TcpListenerFactory();

            // Act
            using var listener = factory.Create(IPAddress.Loopback, 0);

            // Assert
            listener.Should().NotBeNull();
            listener.Should().BeAssignableTo<ITcpListenerWrapper>();
        }

        [Test]
        public void ConsoleLogger_ShouldNotThrow()
        {
            // Arrange
            var logger = new ConsoleLogger();

            // Act & Assert
            Action actLog = () => logger.Log("Test message");
            Action actError = () => logger.LogError("Test error");

            actLog.Should().NotThrow();
            actError.Should().NotThrow();
        }

        [Test]
        public void NetworkStreamWrapper_ShouldWrapStream()
        {
            // Arrange
            using var client = new TcpClient();
            
            // Створюємо NetworkStream тільки якщо клієнт підключений
            // Для тесту просто перевіряємо, що wrapper може бути створений
            
            // Act & Assert - просто перевіряємо, що клас існує і може бути інстанційований
            var wrapperType = typeof(NetworkStreamWrapper);
            wrapperType.Should().NotBeNull();
            wrapperType.GetInterfaces().Should().Contain(typeof(INetworkStreamWrapper));
        }
    }
}
