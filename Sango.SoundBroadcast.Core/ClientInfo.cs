namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 客户端信息
/// </summary>
public class ClientInfo
{
    public string Id { get; set; }
    public string IpAddress { get; set; }
    public DateTime ConnectedTime { get; set; }
    public DateTime LastActivity { get; set; }
    public AudioMetadata Metadata { get; set; }
}