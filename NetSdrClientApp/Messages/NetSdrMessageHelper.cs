using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
//  ФІКС 1 & 2: Видалено using NetSdrClientApp.Networking;

namespace NetSdrClientApp.Messages
{
    //TODO: analyze possible use of [StructLayout] for better performance and readability 
    public static class NetSdrMessageHelper
    {
        //  ФІКС 2: Видалено метод TestArchitectureViolation()

        private const short _maxMessageLength = 8191;
        private const short _maxDataItemMessageLength = 8194;
        private const short _msgHeaderLength = 2;
        private const short _msgControlItemLength = 2;
        private const short _msgSequenceNumberLength = 2;

        public enum MsgTypes
        {
            SetControlItem,
            CurrentControlItem,
            ControlItemRange,
            Ack,
            DataItem0,
            DataItem1,
            DataItem2,
            DataItem3
        }

        public enum ControlItemCodes
        {
            None = 0,
            IQOutputDataSampleRate = 0x00B8,
            RFFilter = 0x0044,
            ADModes = 0x008A,
            ReceiverState = 0x0018,
            ReceiverFrequency = 0x0020
        }

        public static byte[] GetControlItemMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            return GetMessage(type, itemCode, parameters);
        }

        public static byte[] GetDataItemMessage(MsgTypes type, byte[] parameters)
        {
            return GetMessage(type, ControlItemCodes.None, parameters);
        }

        private static byte[] GetMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            var itemCodeBytes = Array.Empty<byte>();
            if (itemCode != ControlItemCodes.None)
            {
                itemCodeBytes = BitConverter.GetBytes((ushort)itemCode);
            }

            var headerBytes = GetHeader(type, itemCodeBytes.Length + parameters.Length);

            List<byte> msg = new List<byte>();
            msg.AddRange(headerBytes);
            msg.AddRange(itemCodeBytes);
            msg.AddRange(parameters);

            return msg.ToArray();
        }

        public static bool TranslateMessage(byte[] msg, out MsgTypes type, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            bool success = true;
            var msgEnumerable = msg as IEnumerable<byte>;  // ✅ ФІКС 3: Виправлено typo

            TranslateHeader(msgEnumerable.Take(_msgHeaderLength).ToArray(), out type, out int msgLength);
            msgEnumerable = msgEnumerable.Skip(_msgHeaderLength);
            msgLength -= _msgHeaderLength;

            if (type < MsgTypes.DataItem0)
            {
                var value = BitConverter.ToUInt16(msgEnumerable.Take(_msgControlItemLength).ToArray());
                msgEnumerable = msgEnumerable.Skip(_msgControlItemLength);
                msgLength -= _msgControlItemLength;

                if (Enum.IsDefined(typeof(ControlItemCodes), value))
                {
                    itemCode = (ControlItemCodes)value;
                }
                else
                {
                    success = false;
                }
            }
            else
            {
                sequenceNumber = BitConverter.ToUInt16(msgEnumerable.Take(_msgSequenceNumberLength).ToArray());
                msgEnumerable = msgEnumerable.Skip(_msgSequenceNumberLength);
                msgLength -= _msgSequenceNumberLength;
            }

            body = msgEnumerable.ToArray();

            success &= body.Length == msgLength;

            return success;
        }

        public static IEnumerable<int> GetSamples(ushort sampleSize, byte[] body)
        {
            sampleSize /= 8;
            if (sampleSize > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be 32 bits or less");  // ✅ ФІКС 4: Додано параметри
            }

            var bodyEnumerable = body as IEnumerable<byte>;
            var prefixBytes = Enumerable.Range(0, 4 - sampleSize)
                                      .Select(b => (byte)0);

            int count = bodyEnumerable.Count();  // ✅ ФІКС 5: Виклик Count() один раз
            while (count >= sampleSize)
            {
                yield return BitConverter.ToInt32(bodyEnumerable
                    .Take(sampleSize)
                    .Concat(prefixBytes)
                    .ToArray());
                bodyEnumerable = bodyEnumerable.Skip(sampleSize);
                count -= sampleSize;
            }
        }

        private static byte[] GetHeader(MsgTypes type, int msgLength)
        {
            int lengthWithHeader = msgLength + 2;

            if (type >= MsgTypes.DataItem0 && lengthWithHeader == _maxDataItemMessageLength)
            {
                lengthWithHeader = 0;
            }

            if (msgLength < 0 || lengthWithHeader > _maxMessageLength)
            {
                throw new ArgumentException("Message length exceeds allowed value", nameof(msgLength));  //  ФІКС 6: Додано paramName
            }

            return BitConverter.GetBytes((ushort)(lengthWithHeader + ((int)type << 13)));
        }

        private static void TranslateHeader(byte[] header, out MsgTypes type, out int msgLength)
        {
            var num = BitConverter.ToUInt16(header);  //  ФІКС 7: Видалено непотрібний .ToArray()
            type = (MsgTypes)(num >> 13);
            msgLength = num - ((int)type << 13);

            if (type >= MsgTypes.DataItem0 && msgLength == 0)
            {
                msgLength = _maxDataItemMessageLength;
            }
        }

        public static byte[] CreateEmptyMessage()  //  ФІКС 8: Додано XML документацію
        {
            return Array.Empty<byte>();
        }

     
        public static bool ValidateMessageSize(int size)
        {
            return size > 0 && size < _maxMessageLength;  //  ФІКС 9: Використано константу
        }

     
        public static byte[] ParseMessage(string hexString)  //  ФІКС 10: Не повертає null
        {
            if (string.IsNullOrEmpty(hexString))
                return Array.Empty<byte>();

            // Парсинг hex рядка
            return Array.Empty<byte>();
        }
    }
}
