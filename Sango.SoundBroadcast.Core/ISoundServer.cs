using System.Net;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频服务器接口
/// </summary>
public interface ISoundServer : IDisposable
{
    /// <summary>
    /// 客户端连接事件
    /// </summary>
    event EventHandler<ClientConnectedEventArgs> ClientConnected;
        
    /// <summary>
    /// 客户端断开事件
    /// </summary>
    event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        
    /// <summary>
    /// 服务器启动事件
    /// </summary>
    event EventHandler ServerStarted;
        
    /// <summary>
    /// 服务器停止事件
    /// </summary>
    event EventHandler ServerStopped;
        
    /// <summary>
    /// 服务器IP地址
    /// </summary>
    IPAddress IpAddress { get; }
        
    /// <summary>
    /// 服务器端口
    /// </summary>
    int Port { get; }
        
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }
        
    /// <summary>
    /// 连接的客户端数量
    /// </summary>
    int ClientCount { get; }
        
    /// <summary>
    /// 音频元数据
    /// </summary>
    AudioMetadata Metadata { get; set; }
        
    /// <summary>
    /// 启动服务器
    /// </summary>
    void Start();
        
    /// <summary>
    /// 停止服务器
    /// </summary>
    void Stop();
        
    /// <summary>
    /// 广播音频数据
    /// </summary>
    /// <param name="audioData">音频数据</param>
    void BroadcastAudioData(byte[] audioData);
        
    /// <summary>
    /// 发送控制命令到所有客户端
    /// </summary>
    /// <param name="command">控制命令</param>
    /// <param name="parameter">参数</param>
    void BroadcastControlCommand(ControlCommand command, float parameter = 0);
        
    /// <summary>
    /// 断开指定客户端
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    void DisconnectClient(string clientId);
        
    /// <summary>
    /// 断开所有客户端
    /// </summary>
    void DisconnectAllClients();
        
    /// <summary>
    /// 获取所有客户端信息
    /// </summary>
    IReadOnlyList<ClientInfo> GetClients();
}