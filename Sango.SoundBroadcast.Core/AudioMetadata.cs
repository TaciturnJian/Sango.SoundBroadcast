using System.Text;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频元数据
/// </summary>
public class AudioMetadata
{
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
    public int BitsPerSample { get; set; } = 16;
    public string ProviderName { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; } = DateTime.Now;
        
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
            
        writer.Write(SampleRate);
        writer.Write(Channels);
        writer.Write(BitsPerSample);
        writer.Write(ProviderName);
        writer.Write(Timestamp.ToBinary());
            
        return ms.ToArray();
    }
        
    public static AudioMetadata Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms, Encoding.UTF8);
            
        return new AudioMetadata
        {
            SampleRate = reader.ReadInt32(),
            Channels = reader.ReadInt32(),
            BitsPerSample = reader.ReadInt32(),
            ProviderName = reader.ReadString(),
            Timestamp = DateTime.FromBinary(reader.ReadInt64())
        };
    }
}