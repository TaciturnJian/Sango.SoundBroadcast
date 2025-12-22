namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 元数据接收事件参数
/// </summary>
public class MetadataReceivedEventArgs : EventArgs
{
    public AudioMetadata Metadata { get; }
        
    public MetadataReceivedEventArgs(AudioMetadata metadata)
    {
        Metadata = metadata;
    }
}