using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace EchoServer.Tests
{
    [TestFixture]
    public class EchoServerTests
    {
        private Mock<ILogger> _loggerMock;
        private Mock<ITcpListener> _listenerMock;
        private Mock<ITcpClient> _clientMock;
        private Mock<NetworkStream> _streamMock;
        private EchoServer _server;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger>();
            _listenerMock = new Mock<ITcpListener>();
            _clientMock = new Mock<ITcpClient>();
            _streamMock = new Mock<NetworkStream>();
            _server = new EchoServer(5000, _loggerMock.Object, _listenerMock.Object);
        }

        [Test]
        public async Task StartAsync_LogsCorrectPort()
        {
            // Arrange
            var endpoint = new IPEndPoint(IPAddress.Any, 5000);
            _listenerMock.Setup(l => l.LocalEndpoint).Returns(endpoint);
            _listenerMock.Setup(l => l.Start());
            _listenerMock.Setup(l => l.AcceptTcpClientAsync())
                        .ThrowsAsync(new ObjectDisposedException("listener"));

            // Act
            var task = _server.StartAsync();
            await Task.Delay(100); // Дати час для запуску
            _server.Stop();

            // Assert
            _loggerMock.Verify(l => l.LogInfo("Server started on port 5000."), Times.Once());
            _loggerMock.Verify(l => l.LogInfo("Server shutdown."), Times.Once());
        }

        [Test]
        public async Task StartAsync_IncrementsActiveConnections_OnClientConnect()
        {
            // Arrange
            var endpoint = new IPEndPoint(IPAddress.Any, 5000);
            _listenerMock.Setup(l => l.LocalEndpoint).Returns(endpoint);
            _listenerMock.Setup(l => l.Start());
            _listenerMock.SetupSequence(l => l.AcceptTcpClientAsync())
                        .ReturnsAsync(new TcpClient())
                        .ThrowsAsync(new ObjectDisposedException("listener"));
            _clientMock.Setup(c => c.GetStream()).Returns(_streamMock.Object);
            _streamMock.Setup(s => s.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(0);

            // Act
            var task = _server.StartAsync();
            await Task.Delay(100); // Дати час для обробки клієнта
            _server.Stop();

            // Assert
            Assert.That(_server.ActiveConnections, Is.EqualTo(0)); // Перевірка, що клієнт оброблений
            _loggerMock.Verify(l => l.LogInfo("Client connected."), Times.Once());
            _loggerMock.Verify(l => l.LogInfo("Client disconnected."), Times.Once());
        }

        [Test]
        public async Task HandleClientAsync_EchoesDataCorrectly()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _clientMock.Setup(c => c.GetStream()).Returns(_streamMock.Object);
            _streamMock.SetupSequence(s => s.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), cancellationToken))
                       .ReturnsAsync(5)
                       .ReturnsAsync(0);
            _streamMock.Setup(s => s.WriteAsync(It.IsAny<byte[]>(), 0, 5, cancellationToken))
                       .Returns(Task.CompletedTask);

            // Act
            await _server.GetType()
                         .GetMethod("HandleClientAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                         .Invoke(_server, new object[] { _clientMock.Object, cancellationToken }) as Task;

            // Assert
            _loggerMock.Verify(l => l.LogInfo("Echoed 5 bytes to the client."), Times.Once());
            _loggerMock.Verify(l => l.LogInfo("Client disconnected."), Times.Once());
        }

        [Test]
        public async Task StopAsync_StopsServerGracefully()
        {
            // Arrange
            var endpoint = new IPEndPoint(IPAddress.Any, 5000);
            _listenerMock.Setup(l => l.LocalEndpoint).Returns(endpoint);
            _listenerMock.Setup(l => l.Start());
            _listenerMock.Setup(l => l.Stop());
            _listenerMock.Setup(l => l.AcceptTcpClientAsync())
                        .ThrowsAsync(new ObjectDisposedException("listener"));

            // Act
            var task = _server.StartAsync();
            await Task.Delay(100);
            await _server.StopAsync(TimeSpan.FromSeconds(1));

            // Assert
            _loggerMock.Verify(l => l.LogInfo("Server stopped."), Times.Once());
            Assert.That(_server.IsRunning, Is.False);
        }

        [Test]
        public void Start_ThrowsIfAlreadyRunning()
        {
            // Arrange
            _listenerMock.Setup(l => l.Start());
            _server.Start();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _server.Start());
        }
    }
}
