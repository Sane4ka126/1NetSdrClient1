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
    public async Task DisconnectTest()
    {
        //Arrange 
        await _client.ConnectAsync();

        //act
        _client.Disconect();

        //assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await _client.ConnectAsync();

        //act
        await _client.StartIQAsync();

        //assert
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await _client.ConnectAsync();

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
        await _client.ConnectAsync();
        long frequency = 14250000; // 14.25 MHz
        int channel = 1;
        int initialCallCount = 3; // ConnectAsync makes 3 calls

        //Act
        await _client.ChangeFrequencyAsync(frequency, channel);

        //Assert - verify one more call was made after ConnectAsync
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(initialCallCount + 1));
    }

    [Test]
    public async Task ChangeFrequencyAsync_NoConnection_ShouldNotSendMessage()
    {
        //Arrange - no connection established
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

        //Act
        await _client.ChangeFrequencyAsync(14250000, 1);

        //Assert - no messages should be sent
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
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
