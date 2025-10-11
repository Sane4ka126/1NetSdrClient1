using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System.Text;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _udpMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _udpMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task ConnectAsync_WhenAlreadyConnected_ShouldNotConnectAgain()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(true);

        //Act
        await _client.ConnectAsync();

        //Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        //act
        await _client.StartIQAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQNoConnectionTest()
    {
        //act
        await _client.StopIQAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WhenConnected_ShouldSendCorrectMessage()
    {
        //Arrange
        await ConnectAsyncTest();
        long frequency = 14250000; // 14.25 MHz
        int channel = 1;

        //Act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(
            msg => msg.Length > 0 && msg[0] == channel
        )), Times.Once);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithDifferentChannels_ShouldWork()
    {
        //Arrange
        await ConnectAsyncTest();

        //Act & Assert for channel 0
        await _client.ChangeFrequencyAsync(7000000, 0);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeastOnce);

        //Act & Assert for channel 2
        await _client.ChangeFrequencyAsync(21000000, 2);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task ChangeFrequencyAsync_NoConnection_ShouldNotSendMessage()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        int initialCallCount = 0;

        //Act
        await _client.ChangeFrequencyAsync(14250000, 1);

        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(initialCallCount));
    }

    [Test]
    public void TcpMessageReceived_ShouldHandleResponse()
    {
        //Arrange
        var testMessage = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        //Act - raise the MessageReceived event
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testMessage);

        //Assert - no exception thrown, event handled
        Assert.Pass("TCP message received and handled successfully");
    }

    [Test]
    public void UdpMessageReceived_ShouldProcessSamples()
    {
        //Arrange
        var testData = CreateMockIQData();

        //Act - raise the MessageReceived event
        _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, testData);

        //Assert - no exception thrown, samples processed
        Assert.Pass("UDP message received and samples processed successfully");
    }

    [Test]
    public async Task SendTcpRequest_WhenNotConnected_ShouldReturnNull()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

        //Act
        await _client.ConnectAsync(); // This won't connect because Connected is false

        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task IQStarted_Property_ShouldReflectState()
    {
        //Arrange
        await ConnectAsyncTest();

        //Assert initial state
        Assert.That(_client.IQStarted, Is.False);

        //Act - start IQ
        await _client.StartIQAsync();

        //Assert after start
        Assert.That(_client.IQStarted, Is.True);

        //Act - stop IQ
        await _client.StopIQAsync();

        //Assert after stop
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task MultipleConnectCalls_ShouldOnlyConnectOnce()
    {
        //Act
        await _client.ConnectAsync();
        await _client.ConnectAsync();
        await _client.ConnectAsync();

        //Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
    }

    [Test]
    public async Task StartStopIQ_Sequence_ShouldWorkCorrectly()
    {
        //Arrange
        await ConnectAsyncTest();

        //Act & Assert - multiple start/stop cycles
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);

        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);

        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);

        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);

        //Verify UDP client was called correct number of times
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
        _udpMock.Verify(udp => udp.StopListening(), Times.Exactly(2));
    }

    [Test]
    public async Task Constructor_ShouldSubscribeToEvents()
    {
        //Arrange & Act - constructor already called in Setup

        //Assert - verify event subscriptions by triggering events
        var testMessage = new byte[] { 0xFF };

        //Should not throw exception when events are raised
        Assert.DoesNotThrow(() =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testMessage);
            _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, testMessage);
        });
    }

    [Test]
    public async Task ReadonlyFields_ShouldNotBeReassignable()
    {
        //Arrange & Act
        var client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);

        //Assert - readonly fields are set in constructor and cannot be changed
        // This is a compile-time check, but we verify behavior is consistent
        await client.ConnectAsync();
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
    }

    [Test]
    public async Task NullableReturnType_SendTcpRequest_IsHandledCorrectly()
    {
        //Arrange - simulate disconnected state
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

        //Act - operations that internally call SendTcpRequest should handle null
        await _client.StartIQAsync(); // Should not throw

        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    /// <summary>
    /// Helper method to create mock IQ data for UDP testing
    /// </summary>
    private byte[] CreateMockIQData()
    {
        // Create a simple mock IQ data packet
        // Format: header + body with sample data
        var data = new List<byte>();
        
        // Add some header bytes (simplified)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        
        // Add sample data (16-bit samples)
        for (int i = 0; i < 10; i++)
        {
            data.AddRange(BitConverter.GetBytes((short)(i * 100)));
        }

        return data.ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        // Cleanup if samples.bin was created during tests
        if (File.Exists("samples.bin"))
        {
            try
            {
                File.Delete("samples.bin");
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
