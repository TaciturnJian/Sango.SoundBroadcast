namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 客户端连接事件参数
/// </summary>
public class ClientConnectedEventArgs : EventArgs
{
    public ClientInfo Client { get; }
        
    public ClientConnectedEventArgs(ClientInfo client)
    {
        Client = client;
    }
}