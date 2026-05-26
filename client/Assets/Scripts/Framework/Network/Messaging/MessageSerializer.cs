using System.IO;
using Google.Protobuf;

namespace YOTO.Network
{
    public interface IMessageSerializer
    {
        int CalculateSize<T>(T msg) where T : IMessage<T>;

        void Serialize<T>(T msg, byte[] dest, int offset) where T : IMessage<T>;

        T Deserialize<T>(byte[] buf, int offset, int length) where T : IMessage<T>, new();
    }

    public sealed class ProtobufMessageSerializer : IMessageSerializer
    {
        public int CalculateSize<T>(T msg) where T : IMessage<T> => msg.CalculateSize();

        public void Serialize<T>(T msg, byte[] dest, int offset) where T : IMessage<T>
        {
            using var ms = new MemoryStream(dest, offset, dest.Length - offset);
            msg.WriteTo(ms);
        }

        public T Deserialize<T>(byte[] buf, int offset, int length) where T : IMessage<T>, new()
        {
            using var ms = new MemoryStream(buf, offset, length, writable: false);
            var parser = new MessageParser<T>(() => new T());
            return parser.ParseFrom(ms);
        }
    }
}
