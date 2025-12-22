using NAudio.CoreAudioApi;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频会话信息
/// </summary>
public class AudioSessionInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; }
    public string DisplayName { get; set; }
    public AudioSessionControl Session { get; set; }
}
