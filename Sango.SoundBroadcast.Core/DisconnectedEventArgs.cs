namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 断开连接事件参数
/// </summary>
public class DisconnectedEventArgs : EventArgs
{
    public string Reason { get; }
        
    public DisconnectedEventArgs(string reason)
    {
        Reason = reason;
    }
}