namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 客户端断开事件参数
/// </summary>
public class ClientDisconnectedEventArgs : EventArgs
{
    public ClientInfo Client { get; }
    public string Reason { get; }
        
    public ClientDisconnectedEventArgs(ClientInfo client, string reason)
    {
        Client = client;
        Reason = reason;
    }
}