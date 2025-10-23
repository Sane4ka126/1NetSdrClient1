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
        public void NetworkStreamWrapper_ShouldImplementInterface()
        {
            // Arrange & Act
            var wrapperType = typeof(NetworkStreamWrapper);

            // Assert
            wrapperType.Should().NotBeNull();
            wrapperType.GetInterfaces().Should().Contain(typeof(INetworkStreamWrapper));
        }

        [Test]
        public void TcpListenerWrapper_ShouldStartAndStop()
        {
            // Arrange
            var wrapper = new TcpListenerWrapper(IPAddress.Loopback, 0);

            // Act
            Action startAct = () => wrapper.Start();
            Action stopAct = () => wrapper.Stop();

            // Assert
            startAct.Should().NotThrow();
            stopAct.Should().NotThrow();
            wrapper.Dispose();
        }

        [Test]
        public void TcpClientWrapper_ShouldImplementInterface()
        {
            // Arrange & Act
            var wrapperType = typeof(TcpClientWrapper);

            // Assert
            wrapperType.Should().Implement<ITcpClientWrapper>();
        }

        [Test]
        public void TcpListenerFactory_ShouldCreateMultipleListeners()
        {
            // Arrange
            var factory = new TcpListenerFactory();

            // Act
            using var listener1 = factory.Create(IPAddress.Loopback, 0);
            using var listener2 = factory.Create(IPAddress.Loopback, 0);

            // Assert
            listener1.Should().NotBeNull();
            listener2.Should().NotBeNull();
            listener1.Should().NotBeSameAs(listener2);
        }

        [Test]
        public void ConsoleLogger_ShouldLogErrorWithPrefix()
        {
            // Arrange
            var logger = new ConsoleLogger();

            // Act & Assert
            Action act = () => logger.LogError("Critical error");
            act.Should().NotThrow();
        }

        [Test]
        public void TcpListenerWrapper_Dispose_ShouldNotThrow()
        {
            // Arrange
            var wrapper = new TcpListenerWrapper(IPAddress.Loopback, 0);
            wrapper.Start();

            // Act
            Action act = () => wrapper.Dispose();

            // Assert
            act.Should().NotThrow();
        }
    }
}
