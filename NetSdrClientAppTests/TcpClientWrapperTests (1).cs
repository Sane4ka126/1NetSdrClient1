using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientApp.Tests.Networking
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        private TcpListener? _testServer;
        private int _testPort;
        private CancellationTokenSource? _serverCts;

        [SetUp]
        public void SetUp()
        {
            // Запускаємо тестовий TCP сервер
            _testPort = GetAvailablePort();
            _testServer = new TcpListener(IPAddress.Loopback, _testPort);
            _serverCts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _serverCts?.Cancel();
            _serverCts?.Dispose();
            _testServer?.Stop();
            
            // Явно dispose TcpListener для задоволення NUnit1032
            if (_testServer is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            _testServer = null;
            _serverCts = null;
        }

        private static int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public void Constructor_SetsHostAndPort()
        {
            // Arrange & Act
            var wrapper = new TcpClientWrapper("localhost", 8080);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(wrapper, Is.Not.Null);
                Assert.That(wrapper.Connected, Is.False);
            });
        }

        [Test]
        public void Connected_ReturnsFalse_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 8080);

            // Act & Assert
            Assert.That(wrapper.Connected, Is.False);
        }

        [Test]
        public async Task Connect_EstablishesConnection_WhenServerIsAvailable()
        {
            // Arrange
            Assert.That(_testServer, Is.Not.Null);
            _testServer.Start();
            var serverTask = AcceptClientAsync(_testServer, _serverCts!.Token);
            var wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act
            wrapper.Connect();
            await Task.Delay(100); // Даємо час на підключення

            // Assert
            Assert.That(wrapper.Connected, Is.True);

            // Cleanup
            wrapper.Disconnect();
            await serverTask;
        }

        [Test]
        public void Connect_DoesNothing_WhenAlreadyConnected()
        {
            // Arrange
            Assert.Multiple(() =>
            {
                Assert.That(_testServer, Is.Not.Null);
                Assert.That(_serverCts, Is.Not.Null);
            });
            
            _testServer.Start();
            var serverTask = AcceptClientAsync(_testServer, _serverCts.Token);
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            wrapper.Connect();
            Task.Delay(100).Wait();

            // Act
            wrapper.Connect(); // Спроба повторного підключення

            // Assert
            Assert.That(wrapper.Connected, Is.True);

            // Cleanup
            wrapper.Disconnect();
        }

        [Test]
        public void Connect_HandlesConnectionFailure_WhenServerNotAvailable()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 9999); // Неіснуючий порт

            // Act
            wrapper.Connect();
            Task.Delay(100).Wait();

            // Assert
            Assert.That(wrapper.Connected, Is.False);
        }

        [Test]
        public async Task Disconnect_ClosesConnection_WhenConnected()
        {
            // Arrange
            Assert.Multiple(() =>
            {
                Assert.That(_testServer, Is.Not.Null);
                Assert.That(_serverCts, Is.Not.Null);
            });
            
            _testServer.Start();
            var serverTask = AcceptClientAsync(_testServer, _serverCts.Token);
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            wrapper.Connect();
            await Task.Delay(100);

            // Act
            wrapper.Disconnect();

            // Assert
            Assert.That(wrapper.Connected, Is.False);
        }

        [Test]
        public void Disconnect_DoesNothing_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", _testPort);

            // Act & Assert (не повинно викликати виключення)
            wrapper.Disconnect();
            Assert.That(wrapper.Connected, Is.False);
        }

        [Test]
        public async Task SendMessageAsync_ByteArray_SendsData_WhenConnected()
        {
            // Arrange
            Assert.Multiple(() =>
            {
                Assert.That(_testServer, Is.Not.Null);
                Assert.That(_serverCts, Is.Not.Null);
            });
            
            _testServer.Start();
            var receivedData = new TaskCompletionSource<byte[]>();
            var serverTask = AcceptAndReceiveAsync(_testServer, receivedData, _serverCts.Token);
            
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            wrapper.Connect();
            await Task.Delay(100);

            byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            await wrapper.SendMessageAsync(testData);

            // Assert
            var received = await receivedData.Task;
            Assert.That(received, Is.EqualTo(testData));

            // Cleanup
            wrapper.Disconnect();
        }

        [Test]
        public async Task SendMessageAsync_String_SendsData_WhenConnected()
        {
            // Arrange
            Assert.Multiple(() =>
            {
                Assert.That(_testServer, Is.Not.Null);
                Assert.That(_serverCts, Is.Not.Null);
            });
            
            _testServer.Start();
            var receivedData = new TaskCompletionSource<byte[]>();
            var serverTask = AcceptAndReceiveAsync(_testServer, receivedData, _serverCts.Token);
            
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            wrapper.Connect();
            await Task.Delay(100);

            string testString = "Hello, World!";
            byte[] expectedData = Encoding.UTF8.GetBytes(testString);

            // Act
            await wrapper.SendMessageAsync(testString);

            // Assert
            var received = await receivedData.Task;
            Assert.That(received, Is.EqualTo(expectedData));

            // Cleanup
            wrapper.Disconnect();
        }

        [Test]
        public void SendMessageAsync_ByteArray_ThrowsException_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            byte[] testData = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync(testData)
            );
        }

        [Test]
        public void SendMessageAsync_String_ThrowsException_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            string testString = "Test";

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync(testString)
            );
        }

        [Test]
        public async Task MessageReceived_Event_RaisedWhenDataReceived()
        {
            // Arrange
            Assert.That(_testServer, Is.Not.Null);
            _testServer.Start();
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            
            byte[]? receivedMessage = null;
            var messageReceivedEvent = new TaskCompletionSource<bool>();
            
            wrapper.MessageReceived += (sender, data) =>
            {
                receivedMessage = data;
                messageReceivedEvent.TrySetResult(true);
            };

            var serverTask = Task.Run(async () =>
            {
                var client = await _testServer.AcceptTcpClientAsync();
                await Task.Delay(200); // Даємо час клієнту підключитись
                var stream = client.GetStream();
                byte[] testData = new byte[] { 0xAA, 0xBB, 0xCC };
                await stream.WriteAsync(testData.AsMemory(0, testData.Length));
                await Task.Delay(100);
                client.Close();
            });

            // Act
            wrapper.Connect();
            await Task.WhenAny(messageReceivedEvent.Task, Task.Delay(2000));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.Not.Null);
                Assert.That(receivedMessage, Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC }));
            });

            // Cleanup
            wrapper.Disconnect();
            await serverTask;
        }

        [Test]
        public async Task MultipleMessages_AreReceivedCorrectly()
        {
            // Arrange
            Assert.That(_testServer, Is.Not.Null);
            _testServer.Start();
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            
            var messagesReceived = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
            var messageCount = new TaskCompletionSource<bool>();
            int expectedMessages = 3;
            
            wrapper.MessageReceived += (sender, data) =>
            {
                messagesReceived.Add(data);
                if (messagesReceived.Count >= expectedMessages)
                {
                    messageCount.TrySetResult(true);
                }
            };

            var serverTask = Task.Run(async () =>
            {
                var client = await _testServer.AcceptTcpClientAsync();
                await Task.Delay(200);
                var stream = client.GetStream();
                
                for (int i = 1; i <= expectedMessages; i++)
                {
                    byte[] testData = new byte[] { (byte)i };
                    await stream.WriteAsync(testData.AsMemory(0, testData.Length));
                    await Task.Delay(50);
                }
                
                await Task.Delay(100);
                client.Close();
            });

            // Act
            wrapper.Connect();
            await Task.WhenAny(messageCount.Task, Task.Delay(3000));

            // Assert
            Assert.That(messagesReceived, Has.Count.EqualTo(expectedMessages));

            // Cleanup
            wrapper.Disconnect();
            await serverTask;
        }

        [Test]
        public async Task Disconnect_StopsListening()
        {
            // Arrange
            Assert.That(_testServer, Is.Not.Null);
            _testServer.Start();
            var wrapper = new TcpClientWrapper("localhost", _testPort);
            bool messageReceived = false;
            
            wrapper.MessageReceived += (sender, data) =>
            {
                messageReceived = true;
            };

            var serverTask = Task.Run(async () =>
            {
                var client = await _testServer.AcceptTcpClientAsync();
                await Task.Delay(200);
                return client;
            });

            wrapper.Connect();
            await Task.Delay(100);
            var serverClient = await serverTask;

            // Act
            wrapper.Disconnect();
            await Task.Delay(100);

            // Спробуємо відправити дані після відключення
            try
            {
                var stream = serverClient.GetStream();
                byte[] data = new byte[] { 0x01 };
                await stream.WriteAsync(data.AsMemory(0, data.Length));
            }
            catch { }

            await Task.Delay(200);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(messageReceived, Is.False); // Не повинні отримати повідомлення після відключення
                Assert.That(wrapper.Connected, Is.False);
            });

            // Cleanup
            serverClient?.Close();
        }

        // Helper methods
        private static async Task AcceptClientAsync(TcpListener server, CancellationToken ct)
        {
            try
            {
                var client = await server.AcceptTcpClientAsync(ct);
                await Task.Delay(500, ct);
                client.Close();
            }
            catch (OperationCanceledException) { }
        }

        private static async Task AcceptAndReceiveAsync(TcpListener server, TaskCompletionSource<byte[]> receivedData, CancellationToken ct)
        {
            try
            {
                var client = await server.AcceptTcpClientAsync(ct);
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                receivedData.SetResult(buffer.Take(bytesRead).ToArray());
                await Task.Delay(100, ct);
                client.Close();
            }
            catch (OperationCanceledException) { }
        }
    }
}
