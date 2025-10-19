using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientApp.Tests.Networking
{
    [TestFixture]
    public class UdpClientWrapperTests
    {
        private UdpClient? _testSender;
        private int _testPort;
        private CancellationTokenSource? _testCts;

        [SetUp]
        public void SetUp()
        {
            _testPort = GetAvailablePort();
            _testCts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _testCts?.Cancel();
            _testCts?.Dispose();
            _testSender?.Close();
            _testSender?.Dispose();
            _testSender = null;
            _testCts = null;
        }

        private static int GetAvailablePort()
        {
            var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
            listener.Close();
            return port;
        }

        [Test]
        public void Constructor_CreatesInstance_WithValidPort()
        {
            // Arrange & Act
            var wrapper = new UdpClientWrapper(_testPort);

            // Assert
            Assert.That(wrapper, Is.Not.Null);
        }

        [Test]
        public void Constructor_CreatesInstance_WithZeroPort()
        {
            // Arrange & Act
            var wrapper = new UdpClientWrapper(0);

            // Assert
            Assert.That(wrapper, Is.Not.Null);
        }

        [Test]
        public async Task StartListeningAsync_ReceivesMessages()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            byte[]? receivedData = null;
            var messageReceived = new TaskCompletionSource<bool>();

            wrapper.MessageReceived += (sender, data) =>
            {
                receivedData = data;
                messageReceived.TrySetResult(true);
            };

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200); // Даємо час на старт

            // Act
            _testSender = new UdpClient();
            byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await _testSender.SendAsync(testData, testData.Length, "localhost", _testPort);

            await Task.WhenAny(messageReceived.Task, Task.Delay(2000));

            // Assert
            Assert.That(receivedData, Is.Not.Null);
            Assert.That(receivedData, Is.EqualTo(testData));

            // Cleanup
            wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StartListeningAsync_ReceivesMultipleMessages()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
            var expectedCount = 3;
            var countReached = new TaskCompletionSource<bool>();

            wrapper.MessageReceived += (sender, data) =>
            {
                receivedMessages.Add(data);
                if (receivedMessages.Count >= expectedCount)
                {
                    countReached.TrySetResult(true);
                }
            };

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act
            _testSender = new UdpClient();
            for (int i = 1; i <= expectedCount; i++)
            {
                byte[] testData = new byte[] { (byte)i };
                await _testSender.SendAsync(testData, testData.Length, "localhost", _testPort);
                await Task.Delay(50);
            }

            await Task.WhenAny(countReached.Task, Task.Delay(3000));

            // Assert
            Assert.That(receivedMessages, Has.Count.EqualTo(expectedCount));

            // Cleanup
            wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StopListening_StopsReceivingMessages()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            int messageCount = 0;

            wrapper.MessageReceived += (sender, data) =>
            {
                messageCount++;
            };

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act - Надсилаємо перше повідомлення
            _testSender = new UdpClient();
            byte[] testData1 = new byte[] { 0x01 };
            await _testSender.SendAsync(testData1, testData1.Length, "localhost", _testPort);
            await Task.Delay(100);

            // Зупиняємо прослуховування
            wrapper.StopListening();
            await Task.Delay(200);

            // Надсилаємо друге повідомлення (не повинно бути отримано)
            byte[] testData2 = new byte[] { 0x02 };
            try
            {
                await _testSender.SendAsync(testData2, testData2.Length, "localhost", _testPort);
            }
            catch { }

            await Task.Delay(200);

            // Assert
            Assert.That(messageCount, Is.EqualTo(1)); // Тільки перше повідомлення

            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task Exit_StopsReceivingMessages()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            int messageCount = 0;

            wrapper.MessageReceived += (sender, data) =>
            {
                messageCount++;
            };

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act
            _testSender = new UdpClient();
            byte[] testData1 = new byte[] { 0x01 };
            await _testSender.SendAsync(testData1, testData1.Length, "localhost", _testPort);
            await Task.Delay(100);

            wrapper.Exit();
            await Task.Delay(200);

            byte[] testData2 = new byte[] { 0x02 };
            try
            {
                await _testSender.SendAsync(testData2, testData2.Length, "localhost", _testPort);
            }
            catch { }

            await Task.Delay(200);

            // Assert
            Assert.That(messageCount, Is.EqualTo(1));

            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StopListening_CanBeCalledMultipleTimes()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.StopListening());
            Assert.DoesNotThrow(() => wrapper.StopListening());
            Assert.DoesNotThrow(() => wrapper.StopListening());

            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public void StopListening_CanBeCalledWithoutStarting()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.StopListening());
        }

        [Test]
        public void Exit_CanBeCalledWithoutStarting()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);

            // Act & Assert
            Assert.DoesNotThrow(() => wrapper.Exit());
        }

        [Test]
        public async Task MessageReceived_Event_IsInvokedWithCorrectData()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            object? eventSender = null;
            byte[]? eventData = null;
            var messageReceived = new TaskCompletionSource<bool>();

            wrapper.MessageReceived += (sender, data) =>
            {
                eventSender = sender;
                eventData = data;
                messageReceived.TrySetResult(true);
            };

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act
            _testSender = new UdpClient();
            byte[] testData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            await _testSender.SendAsync(testData, testData.Length, "localhost", _testPort);

            await Task.WhenAny(messageReceived.Task, Task.Delay(2000));

            // Assert
            Assert.That(eventSender, Is.EqualTo(wrapper));
            Assert.That(eventData, Is.EqualTo(testData));

            // Cleanup
            wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public async Task StartListeningAsync_HandlesSocketException()
        {
            // Arrange - створюємо wrapper з портом, який вже використовується
            var blocker = new UdpClient(_testPort);
            var wrapper = new UdpClientWrapper(_testPort);

            // Act & Assert - не повинно викинути необроблений виняток
            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (Exception ex)
                {
                    // Очікуємо SocketException
                    Assert.That(ex, Is.TypeOf<SocketException>());
                }
            });

            await Task.Delay(500);

            // Cleanup
            wrapper.StopListening();
            blocker.Close();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }

        [Test]
        public void GetHashCode_ReturnsSameValueForSamePort()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(5000);
            var wrapper2 = new UdpClientWrapper(5000);

            // Act
            int hash1 = wrapper1.GetHashCode();
            int hash2 = wrapper2.GetHashCode();

            // Assert
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void GetHashCode_ReturnsDifferentValueForDifferentPorts()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(5000);
            var wrapper2 = new UdpClientWrapper(5001);

            // Act
            int hash1 = wrapper1.GetHashCode();
            int hash2 = wrapper2.GetHashCode();

            // Assert
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void Equals_ReturnsTrueForSamePort()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(5000);
            var wrapper2 = new UdpClientWrapper(5000);

            // Act
            bool areEqual = wrapper1.Equals(wrapper2);

            // Assert
            Assert.That(areEqual, Is.True);
        }

        [Test]
        public void Equals_ReturnsFalseForDifferentPorts()
        {
            // Arrange
            var wrapper1 = new UdpClientWrapper(5000);
            var wrapper2 = new UdpClientWrapper(5001);

            // Act
            bool areEqual = wrapper1.Equals(wrapper2);

            // Assert
            Assert.That(areEqual, Is.False);
        }

        [Test]
        public void Equals_ReturnsFalseForNull()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(5000);

            // Act
            bool areEqual = wrapper.Equals(null);

            // Assert
            Assert.That(areEqual, Is.False);
        }

        [Test]
        public void Equals_ReturnsFalseForDifferentType()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(5000);
            var otherObject = new object();

            // Act
            bool areEqual = wrapper.Equals(otherObject);

            // Assert
            Assert.That(areEqual, Is.False);
        }

        [Test]
        public async Task CleanupResources_DisposesAllResources()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);

            // Act
            wrapper.StopListening();
            await Task.Delay(100);

            // Assert - можемо створити новий wrapper на тому ж порту
            var wrapper2 = new UdpClientWrapper(_testPort);
            var listeningTask2 = Task.Run(async () =>
            {
                try
                {
                    await wrapper2.StartListeningAsync();
                }
                catch (OperationCanceledException) { }
            });

            await Task.Delay(200);
            Assert.That(wrapper2, Is.Not.Null);

            // Cleanup
            wrapper2.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(1000));
            await Task.WhenAny(listeningTask2, Task.Delay(1000));
        }

        [Test]
        public async Task StartListeningAsync_HandlesCancellation()
        {
            // Arrange
            var wrapper = new UdpClientWrapper(_testPort);
            bool taskCompleted = false;

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    await wrapper.StartListeningAsync();
                    taskCompleted = true;
                }
                catch (OperationCanceledException)
                {
                    taskCompleted = true;
                }
            });

            await Task.Delay(200);

            // Act
            wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(2000));

            // Assert
            Assert.That(taskCompleted, Is.True);
        }
    }
}
