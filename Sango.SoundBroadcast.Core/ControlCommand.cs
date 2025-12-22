namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 控制命令类型
/// </summary>
public enum ControlCommand : byte
{
    Start = 0x01,          // 开始传输
    Stop = 0x02,           // 停止传输
    Pause = 0x03,          // 暂停传输
    Resume = 0x04,         // 恢复传输
    SetVolume = 0x05,      // 设置音量
    RequestMetadata = 0x06 // 请求元数据
}