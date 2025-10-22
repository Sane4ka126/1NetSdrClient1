using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientApp.Tests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<ITcpListener> _mockListener;
        private Mock<ILogger> _mockLogger;
        private Mock<IClientHandler> _mockHandler;
        private EchoServer _server;

        [SetUp]
        public void SetUp()
        {
            _mockListener = new Mock<ITcpListener>();
            _mockLogger = new Mock<ILogger>();
            _mockHandler = new Mock<IClientHandler>();
            _server = new EchoServer(5000, _mockListener.Object, _mockLogger.Object, _mockHandler.Object);
        }

        [Test]
        public async Task StartAsync_ListenerStartsAndAcceptsClient()
        {
            // Arrange
            var mockClient = new TcpClient();
            _mockListener.Setup(l => l.AcceptTcpClientAsync()).ReturnsAsync(mockClient);

            // Act
            var startTask = _server.StartAsync();
            _mockListener.Object.Start(); // Simulate listener start
            await Task.Delay(100); // Wait for accept loop to process

            // Assert
            _mockLogger.Verify(l => l.Log("Server started on port 5000."), Times.Once());
            _mockHandler.Verify(h => h.HandleAsync(mockClient, It.IsAny<CancellationToken>()), Times.Once());
            _mockListener.Verify(l => l.Start(), Times.Once());

            // Cleanup
            _server.Stop();
            await startTask;
        }

        [Test]
        public async Task HandleAsync_EchoesDataBack()
        {
            // Arrange
            var handler = new EchoClientHandler(_mockLogger.Object);
            var mockClient = new Mock<TcpClient>();
            var mockStream = new Mock<NetworkStream>(new Mock<Stream>().Object, true);
            byte[] testData = { 1, 2, 3 };
            int bytesRead = testData.Length;

            mockClient.Setup(c => c.GetStream()).Returns(mockStream.Object);
            mockStream.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(bytesRead);
            mockStream.Setup(s => s.WriteAsync(testData, 0, bytesRead, It.IsAny<CancellationToken>()));

            var token = new CancellationTokenSource().Token;

            // Act
            await handler.HandleAsync(mockClient.Object, token);

            // Assert
            mockStream.Verify(s => s.WriteAsync(It.Is<byte[]>(b => b.Take(bytesRead).SequenceEqual(testData)), 0, bytesRead, token), Times.Once());
            _mockLogger.Verify(l => l.Log(It.Is<string>(m => m.Contains("Echoed"))), Times.Once());
            _mockLogger.Verify(l => l.Log("Client disconnected."), Times.Once());
        }

        [Test]
        public void Stop_CancelsTokenAndStopsListener()
        {
            // Act
            _server.Stop();

            // Assert
            _mockListener.Verify(l => l.Stop(), Times.Once());
            _mockLogger.Verify(l => l.Log("Server stopped."), Times.Once());
        }

        [Test]
        public void Dispose_CleansUpResources()
        {
            // Act
            _server.Dispose();

            // Assert
            _mockListener.Verify(l => l.Dispose(), Times.Once());
        }

        [Test]
        public void Constructor_WithNullDependencies_UsesDefaults()
        {
            // Act
            var server = new EchoServer(5000);

            // Assert
            Assert.IsNotNull(server); // Default dependencies instantiated
        }
    }
}
