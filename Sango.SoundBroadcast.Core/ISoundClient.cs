namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频客户端接口
/// </summary>
public interface ISoundClient : IDisposable
{
    /// <summary>
    /// 连接成功事件
    /// </summary>
    event EventHandler Connected;
        
    /// <summary>
    /// 连接断开事件
    /// </summary>
    event EventHandler<DisconnectedEventArgs> Disconnected;
        
    /// <summary>
    /// 音频数据接收事件
    /// </summary>
    event EventHandler<AudioDataReceivedEventArgs> AudioDataReceived;
        
    /// <summary>
    /// 元数据接收事件
    /// </summary>
    event EventHandler<MetadataReceivedEventArgs> MetadataReceived;
        
    /// <summary>
    /// 控制命令接收事件
    /// </summary>
    event EventHandler<ControlCommandReceivedEventArgs> ControlCommandReceived;
        
    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }
        
    /// <summary>
    /// 服务器地址
    /// </summary>
    string ServerAddress { get; }
        
    /// <summary>
    /// 服务器端口
    /// </summary>
    int ServerPort { get; }
        
    /// <summary>
    /// 客户端ID
    /// </summary>
    string ClientId { get; }
        
    /// <summary>
    /// 音频元数据
    /// </summary>
    AudioMetadata Metadata { get; }
        
    /// <summary>
    /// 连接服务器
    /// </summary>
    void Connect();
        
    /// <summary>
    /// 断开连接
    /// </summary>
    void Disconnect();
        
    /// <summary>
    /// 发送控制命令
    /// </summary>
    /// <param name="command">控制命令</param>
    /// <param name="parameter">参数</param>
    void SendControlCommand(ControlCommand command, float parameter = 0);
        
    /// <summary>
    /// 请求元数据
    /// </summary>
    void RequestMetadata();
}