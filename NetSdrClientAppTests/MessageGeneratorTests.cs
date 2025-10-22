using System;
using System.Linq;
using EchoServer.Wrappers;
using FluentAssertions;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class MessageGeneratorTests
    {
        [Test]
        public void GenerateMessage_ShouldReturnCorrectMessageFormat()
        {
            var generator = new RandomMessageGenerator();
            ushort sequenceNumber = 42;

            var message = generator.GenerateMessage(sequenceNumber);

            message.Should().NotBeNull();
            message.Length.Should().Be(1028);
            message[0].Should().Be(0x04);
            message[1].Should().Be(0x84);
        }

        [Test]
        public void GenerateMessage_ShouldIncludeSequenceNumber()
        {
            var generator = new RandomMessageGenerator();
            ushort sequenceNumber = 12345;

            var message = generator.GenerateMessage(sequenceNumber);

            var extractedSequence = BitConverter.ToUInt16(message, 2);
            extractedSequence.Should().Be(sequenceNumber);
        }

        [Test]
        public void GenerateMessage_ShouldGenerateRandomData()
        {
            var generator = new RandomMessageGenerator();
            
            var message1 = generator.GenerateMessage(1);
            var message2 = generator.GenerateMessage(1);

            var data1 = message1.Skip(4).ToArray();
            var data2 = message2.Skip(4).ToArray();

            data1.Should().NotEqual(data2);
        }

        [Test]
        public void GenerateMessage_ShouldHandleMaxSequenceNumber()
        {
            var generator = new RandomMessageGenerator();
            ushort maxSequence = ushort.MaxValue;

            var message = generator.GenerateMessage(maxSequence);

            message.Should().NotBeNull();
            var extractedSequence = BitConverter.ToUInt16(message, 2);
            extractedSequence.Should().Be(maxSequence);
        }
    }
}
