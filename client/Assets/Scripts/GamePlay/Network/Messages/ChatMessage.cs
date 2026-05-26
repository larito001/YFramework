using System;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace YOTO.Gameplay.Net
{
    /// 手写的 protobuf 消息（项目里没有 protoc 工具链，先用手写撑起 chat smoke test）。
    /// Wire 格式等价于：
    ///   message ChatMessage {
    ///     string sender    = 1;
    ///     string text      = 2;
    ///     int64  timestamp = 3;
    ///   }
    /// 后续若引入 protoc，可以直接用同样字段号生成的版本替换本文件，二进制兼容。
    public sealed class ChatMessage : IMessage<ChatMessage>
    {
        private static readonly MessageParser<ChatMessage> _parser =
            new MessageParser<ChatMessage>(() => new ChatMessage());

        public static MessageParser<ChatMessage> Parser => _parser;

        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public long Timestamp { get; set; }

        public ChatMessage() { }

        public ChatMessage(ChatMessage other)
        {
            Sender = other.Sender;
            Text = other.Text;
            Timestamp = other.Timestamp;
        }

        public ChatMessage Clone() => new ChatMessage(this);

        public bool Equals(ChatMessage other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(other, this)) return true;
            return Sender == other.Sender && Text == other.Text && Timestamp == other.Timestamp;
        }

        public override bool Equals(object obj) => Equals(obj as ChatMessage);

        public override int GetHashCode()
        {
            int hash = 17;
            if (Sender.Length != 0) hash = hash * 31 + Sender.GetHashCode();
            if (Text.Length != 0) hash = hash * 31 + Text.GetHashCode();
            if (Timestamp != 0L) hash = hash * 31 + Timestamp.GetHashCode();
            return hash;
        }

        public override string ToString() => $"ChatMessage[{Timestamp}] {Sender}: {Text}";

        // 本消息不走 protoc 生成，没有真正的 descriptor。
        // 当前的 NetworkSession 序列化 pipeline 只调 CalculateSize/WriteTo/MergeFrom，不会查 Descriptor。
        // 若有反射 / JsonFormatter 等场景来查，再换成 protoc 生成版本即可。
        public MessageDescriptor Descriptor
            => throw new NotSupportedException("ChatMessage 没有 protoc 生成的 descriptor");

        public int CalculateSize()
        {
            int size = 0;
            if (Sender.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(Sender);
            if (Text.Length != 0) size += 1 + CodedOutputStream.ComputeStringSize(Text);
            if (Timestamp != 0L) size += 1 + CodedOutputStream.ComputeInt64Size(Timestamp);
            return size;
        }

        public void WriteTo(CodedOutputStream output)
        {
            if (Sender.Length != 0)
            {
                output.WriteRawTag(10);   // field 1, wire type 2 (length-delimited)
                output.WriteString(Sender);
            }
            if (Text.Length != 0)
            {
                output.WriteRawTag(18);   // field 2, wire type 2
                output.WriteString(Text);
            }
            if (Timestamp != 0L)
            {
                output.WriteRawTag(24);   // field 3, wire type 0 (varint)
                output.WriteInt64(Timestamp);
            }
        }

        public void MergeFrom(ChatMessage other)
        {
            if (other == null) return;
            if (other.Sender.Length != 0) Sender = other.Sender;
            if (other.Text.Length != 0) Text = other.Text;
            if (other.Timestamp != 0L) Timestamp = other.Timestamp;
        }

        public void MergeFrom(CodedInputStream input)
        {
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    default:
                        input.SkipLastField();
                        break;
                    case 10:
                        Sender = input.ReadString();
                        break;
                    case 18:
                        Text = input.ReadString();
                        break;
                    case 24:
                        Timestamp = input.ReadInt64();
                        break;
                }
            }
        }
    }
}
