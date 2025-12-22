namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频数据接收事件参数
/// </summary>
public class AudioDataReceivedEventArgs : EventArgs
{
    public byte[] AudioData { get; }
    public int SequenceNumber { get; }
    public DateTime Timestamp { get; }
        
    public AudioDataReceivedEventArgs(byte[] audioData, int sequenceNumber, DateTime timestamp)
    {
        AudioData = audioData;
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
    }
}