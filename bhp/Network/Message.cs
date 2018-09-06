using Bhp.Cryptography;
using Bhp.IO;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bhp.Network
{
    public class Message : ISerializable
    {
        private const int PayloadMaxSize = 0x02000000;

        /// <summary>
        /// 协议标识号 uint32
        /// </summary>
        public static readonly uint Magic = Settings.Default.Magic;
        /// <summary>
        /// 命令 char[12]
        /// </summary>
        public string Command;
        /// <summary>
        ///  Payload的长度 uint32
        /// </summary>
        //public int Length;

        /// <summary>
        ///  校验和 uint32
        /// </summary>
        public uint Checksum;
        /// <summary>
        /// 消息内容 uint8[length]
        /// </summary>
        public byte[] Payload;

        /// <summary>
        /// 数据包的长度（整体）
        /// </summary>
        public int Size => sizeof(uint) + 12 + sizeof(int) + sizeof(uint) + Payload.Length;

        public static Message Create(string command, ISerializable payload = null)
        {
            return Create(command, payload == null ? new byte[0] : payload.ToArray());
        }

        public static Message Create(string command, byte[] payload)
        {
            return new Message
            {
                Command = command,
                Checksum = GetChecksum(payload),
                Payload = payload
            };
        }

        /// <summary>
        /// 从字节流中解析出数据包
        /// </summary>
        /// <param name="reader">二进制流</param>
        void ISerializable.Deserialize(BinaryReader reader)
        {
            if (reader.ReadUInt32() != Magic)
                throw new FormatException();
            this.Command = reader.ReadFixedString(12);
            uint length = reader.ReadUInt32();
            if (length > PayloadMaxSize)
                throw new FormatException();
            this.Checksum = reader.ReadUInt32();
            this.Payload = reader.ReadBytes((int)length);
            if (GetChecksum(Payload) != Checksum)
                throw new FormatException();
        }

        /// <summary>
        /// 异步读取字节流并解析数据包
        /// <para>读数据包头,24个字节</para>
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="cancellationToken">取消操作的通知</param>
        /// <returns></returns>
        public static async Task<Message> DeserializeFromAsync(Stream stream, CancellationToken cancellationToken)
        {
            uint payload_length;
            byte[] buffer = await FillBufferAsync(stream, 24, cancellationToken);
            Message message = new Message();
            using (MemoryStream ms = new MemoryStream(buffer, false))
            using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8))
            {
                if (reader.ReadUInt32() != Magic)
                    throw new FormatException();
                message.Command = reader.ReadFixedString(12);
                payload_length = reader.ReadUInt32();
                if (payload_length > PayloadMaxSize)
                    throw new FormatException();
                message.Checksum = reader.ReadUInt32();
            }
            if (payload_length > 0)
                message.Payload = await FillBufferAsync(stream, (int)payload_length, cancellationToken);
            else
                message.Payload = new byte[0];
            if (GetChecksum(message.Payload) != message.Checksum)
                throw new FormatException();
            return message;
        }

        /// <summary>
        /// 从WebSocket中异步解析数据
        /// <para>首先读数据包头，24个字节</para>
        /// <para>解析成功后再根据消息长度，读取数据包体</para>
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<Message> DeserializeFromAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            uint payload_length;
            byte[] buffer = await FillBufferAsync(socket, 24, cancellationToken);
            Message message = new Message();
            using (MemoryStream ms = new MemoryStream(buffer, false))
            using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8))
            {
                if (reader.ReadUInt32() != Magic)
                    throw new FormatException();
                message.Command = reader.ReadFixedString(12);
                payload_length = reader.ReadUInt32();
                if (payload_length > PayloadMaxSize)
                    throw new FormatException();
                message.Checksum = reader.ReadUInt32();
            }
            if (payload_length > 0)
                message.Payload = await FillBufferAsync(socket, (int)payload_length, cancellationToken);
            else
                message.Payload = new byte[0];
            if (GetChecksum(message.Payload) != message.Checksum)
                throw new FormatException();
            return message;
        }

        /// <summary>
        /// 从字流节中异步读取字节，并监视取消操作
        /// </summary>
        /// <param name="stream">字节流</param>
        /// <param name="buffer_size">需要读取的长度</param>
        /// <param name="cancellationToken">传播取消通知的令牌</param>
        /// <returns></returns>
        private static async Task<byte[]> FillBufferAsync(Stream stream, int buffer_size, CancellationToken cancellationToken)
        {
            const int MAX_SIZE = 1024;
            byte[] buffer = new byte[buffer_size < MAX_SIZE ? buffer_size : MAX_SIZE];
            using (MemoryStream ms = new MemoryStream())
            {
                while (buffer_size > 0)
                {
                    int count = buffer_size < MAX_SIZE ? buffer_size : MAX_SIZE;
                    count = await stream.ReadAsync(buffer, 0, count, cancellationToken);
                    if (count <= 0) throw new IOException();
                    ms.Write(buffer, 0, count);
                    buffer_size -= count;
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 从WebSocket中异步读取数据
        /// </summary>
        /// <param name="socket">WebSocket</param>
        /// <param name="buffer_size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task<byte[]> FillBufferAsync(WebSocket socket, int buffer_size, CancellationToken cancellationToken)
        {
            const int MAX_SIZE = 1024;
            byte[] buffer = new byte[buffer_size < MAX_SIZE ? buffer_size : MAX_SIZE];
            using (MemoryStream ms = new MemoryStream())
            {
                while (buffer_size > 0)
                {
                    int count = buffer_size < MAX_SIZE ? buffer_size : MAX_SIZE;
                    ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, count);
                    WebSocketReceiveResult result = await socket.ReceiveAsync(segment, cancellationToken);
                    if (result.Count <= 0 || result.MessageType != WebSocketMessageType.Binary)
                        throw new IOException();
                    ms.Write(buffer, 0, result.Count);
                    buffer_size -= result.Count;
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 求字节流的哈希值
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static uint GetChecksum(byte[] value)
        {
            return Crypto.Default.Hash256(value).ToUInt32(0);
        }

        /// <summary>
        /// 按网络协议打包数据
        /// </summary>
        /// <param name="writer"></param>
        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Magic);
            writer.WriteFixedString(Command, 12);
            writer.Write(Payload.Length);
            writer.Write(Checksum);
            writer.Write(Payload);
        }
    }
}
