namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频协议消息类型
/// </summary>
public enum MessageType : byte
{
    AudioData = 0x01,      // 音频数据
    Metadata = 0x02,       // 元数据（采样率、声道数等）
    Control = 0x03,        // 控制命令
    Heartbeat = 0x04       // 心跳包
}