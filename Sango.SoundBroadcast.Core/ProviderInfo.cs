namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频提供者信息
/// </summary>
public class ProviderInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public ISoundProvider Provider { get; set; }
    public float Gain { get; set; }
    public bool Enabled { get; set; }
    public DateTime AddedTime { get; set; }
    public int BytesProcessed { get; set; }
}