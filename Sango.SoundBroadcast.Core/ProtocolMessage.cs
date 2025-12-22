using System.Text;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 协议消息
/// </summary>
public class ProtocolMessage
{
    public MessageType Type { get; set; }
    public byte[] Data { get; set; }
    public int SequenceNumber { get; set; }
        
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
            
        // 头部：类型(1) + 序列号(4) + 数据长度(4)
        writer.Write((byte)Type);
        writer.Write(SequenceNumber);
        writer.Write(Data?.Length ?? 0);
            
        // 数据体
        if (Data != null && Data.Length > 0)
        {
            writer.Write(Data);
        }
            
        return ms.ToArray();
    }
        
    public static ProtocolMessage Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms, Encoding.UTF8);
            
        var message = new ProtocolMessage
        {
            Type = (MessageType)reader.ReadByte(),
            SequenceNumber = reader.ReadInt32()
        };
            
        int dataLength = reader.ReadInt32();
        if (dataLength > 0)
        {
            message.Data = reader.ReadBytes(dataLength);
        }
            
        return message;
    }
        
    // 创建控制消息的辅助方法
    public static ProtocolMessage CreateControlMessage(ControlCommand command, float parameter = 0)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
            
        writer.Write((byte)command);
        writer.Write(parameter);
            
        return new ProtocolMessage
        {
            Type = MessageType.Control,
            Data = ms.ToArray()
        };
    }
        
    // 创建音频数据消息的辅助方法
    public static ProtocolMessage CreateAudioMessage(byte[] audioData, int sequenceNumber)
    {
        return new ProtocolMessage
        {
            Type = MessageType.AudioData,
            Data = audioData,
            SequenceNumber = sequenceNumber
        };
    }
        
    // 创建元数据消息的辅助方法
    public static ProtocolMessage CreateMetadataMessage(AudioMetadata metadata)
    {
        return new ProtocolMessage
        {
            Type = MessageType.Metadata,
            Data = metadata.Serialize()
        };
    }
        
    // 创建心跳消息的辅助方法
    public static ProtocolMessage CreateHeartbeatMessage()
    {
        return new ProtocolMessage
        {
            Type = MessageType.Heartbeat,
            Data = Array.Empty<byte>()
        };
    }
}