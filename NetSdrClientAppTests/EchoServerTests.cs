using System;
using System.Threading;
using System.Threading.Tasks;
using EchoServer.Interfaces;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<ITcpListenerFactory> _mockFactory;
        private Mock<ITcpListener> _mockListener;
        private Mock<ILogger> _mockLogger;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = new Mock<ITcpListenerFactory>();
            _mockListener = new Mock<ITcpListener>();
            _mockLogger = new Mock<ILogger>();
            
            _mockFactory.Setup(f => f.CreateListener(It.IsAny<int>())).Returns(_mockListener.Object);
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenFactoryIsNull()
        {
            Action act = () => new EchoServer.EchoServer(5000, null, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("listenerFactory");
        }

        [Test]
        public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
        {
            Action act = () => new EchoServer.EchoServer(5000, _mockFactory.Object, null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Test]
        public async Task StartAsync_ShouldStartListener()
        {
            var cts = new CancellationTokenSource();
            _mockListener.Setup(l => l.AcceptTcpClientAsync())
                .Returns(async () =>
                {
                    await Task.Delay(100);
                    cts.Cancel();
                    throw new ObjectDisposedException("listener");
                });

            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            var serverTask = server.StartAsync();

            await Task.Delay(50);
            cts.Cancel();
            server.Stop();

            _mockListener.Verify(l => l.Start(), Times.Once);
            _mockLogger.Verify(l => l.Log("Server started on port 5000."), Times.Once);
        }

        [Test]
        public async Task StartAsync_ShouldIncrementClientsHandled_WhenClientConnects()
        {
            var mockClient = new Mock<ITcpClient>();
            var mockStream = new Mock<INetworkStream>();
            
            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            var callCount = 0;
            _mockListener.Setup(l => l.AcceptTcpClientAsync())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount == 1) return mockClient.Object;
                    throw new ObjectDisposedException("listener");
                });

            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            var serverTask = Task.Run(() => server.StartAsync());

            await Task.Delay(100);
            server.Stop();
            await Task.Delay(100);

            server.ClientsHandled.Should().Be(1);
        }

        [Test]
        public async Task ProcessEchoStreamAsync_ShouldEchoDataBack()
        {
            var mockStream = new Mock<INetworkStream>();
            var receivedData = new byte[] { 1, 2, 3, 4, 5 };
            byte[] echoedData = null;

            mockStream.SetupSequence(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    Array.Copy(receivedData, 0, buffer, offset, receivedData.Length);
                    return receivedData.Length;
                })
                .ReturnsAsync(0);

            mockStream.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Callback<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    echoedData = new byte[count];
                    Array.Copy(buffer, offset, echoedData, 0, count);
                })
                .Returns(Task.CompletedTask);

            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            await server.ProcessEchoStreamAsync(mockStream.Object, CancellationToken.None);

            echoedData.Should().NotBeNull();
            echoedData.Should().Equal(receivedData);
            _mockLogger.Verify(l => l.Log($"Echoed {receivedData.Length} bytes to the client."), Times.Once);
        }

        [Test]
        public void ProcessEchoStreamAsync_ShouldStopWhenCancellationRequested()
        {
            var mockStream = new Mock<INetworkStream>();
            var cts = new CancellationTokenSource();

            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], int, int, CancellationToken>((buffer, offset, count, token) =>
                {
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(5);
                });

            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await server.ProcessEchoStreamAsync(mockStream.Object, cts.Token));
        }

        [Test]
        public async Task HandleClientAsync_ShouldCloseClient_AfterProcessing()
        {
            var mockClient = new Mock<ITcpClient>();
            var mockStream = new Mock<INetworkStream>();

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            await server.HandleClientAsync(mockClient.Object, CancellationToken.None);

            mockClient.Verify(c => c.Close(), Times.Once);
            _mockLogger.Verify(l => l.Log("Client disconnected."), Times.Once);
        }

        [Test]
        public void Stop_ShouldStopListener()
        {
            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            _ = Task.Run(() => server.StartAsync());

            Task.Delay(50).Wait();
            server.Stop();

            _mockListener.Verify(l => l.Stop(), Times.Once);
            _mockLogger.Verify(l => l.Log("Server stopped."), Times.Once);
        }

        [Test]
        public void Stop_ShouldNotThrow_WhenCalledMultipleTimes()
        {
            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            
            server.Stop();
            Action act = () => server.Stop();
            
            act.Should().NotThrow();
        }

        [Test]
        public void IsRunning_ShouldBeFalse_Initially()
        {
            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            server.IsRunning.Should().BeFalse();
        }

        [Test]
        public void ClientsHandled_ShouldBeZero_Initially()
        {
            var server = new EchoServer.EchoServer(5000, _mockFactory.Object, _mockLogger.Object);
            server.ClientsHandled.Should().Be(0);
        }
    }
}
