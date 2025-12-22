namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频数据可用事件参数
/// </summary>
public class AudioDataAvailableEventArgs(byte[] audioData, int length) : EventArgs
{
    /// <summary>
    /// 音频数据缓冲区
    /// </summary>
    public byte[] AudioData { get; } = audioData;

    /// <summary>
    /// 数据长度（字节数）
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.Now;
}