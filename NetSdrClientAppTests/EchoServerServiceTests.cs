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
    public class EchoServerServiceTests
    {
        private Mock<ILogger>? _mockLogger;
        private Mock<ITcpListenerFactory>? _mockListenerFactory;
        private Mock<ITcpListenerWrapper>? _mockListener;

        [SetUp]
        public void SetUp()
        {
            _mockLogger = new Mock<ILogger>();
            _mockListenerFactory = new Mock<ITcpListenerFactory>();
            _mockListener = new Mock<ITcpListenerWrapper>();
            
            _mockListenerFactory
                .Setup(f => f.Create(It.IsAny<System.Net.IPAddress>(), It.IsAny<int>()))
                .Returns(_mockListener.Object);
        }

        [Test]
        public async Task HandleClientAsync_ShouldEchoReceivedData()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            byte[] receivedData = new byte[] { 1, 2, 3, 4, 5 };
            byte[]? echoedData = null;
            int readCallCount = 0;

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    if (readCallCount == 0)
                    {
                        Array.Copy(receivedData, 0, buffer, offset, receivedData.Length);
                        readCallCount++;
                        return receivedData.Length;
                    }
                    return 0;
                });

            mockStream
                .Setup(s => s.WriteAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    echoedData = new byte[count];
                    Array.Copy(buffer, offset, echoedData, 0, count);
                })
                .Returns(Task.CompletedTask);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            echoedData.Should().NotBeNull();
            echoedData.Should().BeEquivalentTo(receivedData);
            _mockLogger!.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed 5 bytes"))), Times.Once);
            _mockLogger.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldHandleEmptyStream()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            mockStream.Verify(
                s => s.WriteAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()), 
                Times.Never);
            _mockLogger!.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldHandleException()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            _mockLogger!.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Network error"))), Times.Once);
            _mockLogger.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldEchoMultipleMessages()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            byte[] message1 = new byte[] { 1, 2, 3 };
            byte[] message2 = new byte[] { 4, 5, 6, 7 };
            int readCallCount = 0;

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    if (readCallCount == 0)
                    {
                        Array.Copy(message1, 0, buffer, offset, message1.Length);
                        readCallCount++;
                        return message1.Length;
                    }
                    else if (readCallCount == 1)
                    {
                        Array.Copy(message2, 0, buffer, offset, message2.Length);
                        readCallCount++;
                        return message2.Length;
                    }
                    return 0;
                });

            int writeCount = 0;
            mockStream
                .Setup(s => s.WriteAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .Callback(() => writeCount++)
                .Returns(Task.CompletedTask);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            writeCount.Should().Be(2);
            _mockLogger!.Verify(l => l.Log(It.Is<string>(s => s.Contains("Echoed"))), Times.Exactly(2));
        }

        [Test]
        public void Constructor_ShouldThrowException_WhenLoggerIsNull()
        {
            // Arrange & Act
            Action act = () => new EchoServerService(5000, null!, _mockListenerFactory!.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public void Constructor_ShouldThrowException_WhenListenerFactoryIsNull()
        {
            // Arrange & Act
            Action act = () => new EchoServerService(5000, _mockLogger!.Object, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("listenerFactory");
        }

        [Test]
        public async Task HandleClientAsync_ShouldStopOnCancellation()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();
            var cts = new CancellationTokenSource();

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(), 
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);

            // Act
            cts.Cancel();
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            _mockLogger!.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public void Stop_ShouldCancelTokenAndStopListener()
        {
            // Arrange
            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);

            // Act
            server.Stop();

            // Assert
            _mockLogger!.Verify(l => l.Log("Server stopped."), Times.Once);
        }

        [Test]
        public async Task StartAsync_ShouldStartListenerAndLogMessage()
        {
            // Arrange
            _mockListener!.Setup(l => l.AcceptTcpClientAsync())
                .ThrowsAsync(new ObjectDisposedException("test"));

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);

            // Act
            var startTask = Task.Run(() => server.StartAsync());
            await Task.Delay(100);
            server.Stop();
            await startTask;

            // Assert
            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockLogger!.Verify(l => l.Log(It.Is<string>(s => s.Contains("Server started on port 5000"))), Times.Once);
            _mockLogger.Verify(l => l.Log("Server shutdown."), Times.Once);
        }

        [Test]
        public async Task StartAsync_ShouldHandleAcceptClientException()
        {
            // Arrange
            _mockListener!.Setup(l => l.AcceptTcpClientAsync())
                .ThrowsAsync(new Exception("Test exception"));

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);

            // Act
            await server.StartAsync();

            // Assert
            _mockLogger!.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Test exception"))), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldCloseClientOnOperationCanceled()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            mockClient.Verify(c => c.Close(), Times.Once);
            _mockLogger!.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task HandleClientAsync_ShouldHandleLargeData()
        {
            // Arrange
            var mockClient = new Mock<ITcpClientWrapper>();
            var mockStream = new Mock<INetworkStreamWrapper>();

            byte[] largeData = new byte[8192]; // Full buffer
            for (int i = 0; i < largeData.Length; i++)
                largeData[i] = (byte)(i % 256);

            byte[]? echoedData = null;
            int readCallCount = 0;

            mockStream
                .Setup(s => s.ReadAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    if (readCallCount == 0)
                    {
                        Array.Copy(largeData, 0, buffer, offset, largeData.Length);
                        readCallCount++;
                        return largeData.Length;
                    }
                    return 0;
                });

            mockStream
                .Setup(s => s.WriteAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    echoedData = new byte[count];
                    Array.Copy(buffer, offset, echoedData, 0, count);
                })
                .Returns(Task.CompletedTask);

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);

            var server = new EchoServerService(5000, _mockLogger!.Object, _mockListenerFactory!.Object);
            var cts = new CancellationTokenSource();

            // Act
            await server.HandleClientAsync(mockClient.Object, cts.Token);

            // Assert
            echoedData.Should().NotBeNull();
            echoedData.Should().HaveCount(8192);
            echoedData.Should().BeEquivalentTo(largeData);
        }
    }
}
