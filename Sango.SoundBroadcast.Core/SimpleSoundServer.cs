using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 简单的TCP音频服务器
/// </summary>
public class SimpleSoundServer : ISoundServer
{
    private TcpListener _listener;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, ClientSession> _clients;
    private int _sequenceNumber = 0;
    private Task _acceptTask;
    private Timer _heartbeatTimer;
        
    public event EventHandler<ClientConnectedEventArgs> ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
    public event EventHandler ServerStarted;
    public event EventHandler ServerStopped;
        
    public IPAddress IpAddress { get; }
    public int Port { get; }
    public bool IsRunning { get; private set; }
    public int ClientCount => _clients.Count;
    public AudioMetadata Metadata { get; set; }
        
    public SimpleSoundServer(IPAddress ipAddress, int port)
    {
        IpAddress = ipAddress ?? IPAddress.Any;
        Port = port;
        _clients = new ConcurrentDictionary<string, ClientSession>();
        Metadata = new AudioMetadata();
    }
        
    public SimpleSoundServer(string ipAddress, int port) 
        : this(IPAddress.Parse(ipAddress), port)
    {
    }
        
    public SimpleSoundServer(int port) 
        : this(IPAddress.Any, port)
    {
    }
        
    public void Start()
    {
        if (IsRunning)
            return;
            
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
                
            // 创建TCP监听器
            _listener = new TcpListener(IpAddress, Port);
            _listener.Start();
                
            IsRunning = true;
                
            // 启动客户端接受任务
            _acceptTask = Task.Run(AcceptClientsAsync, _cancellationTokenSource.Token);
                
            // 启动心跳定时器
            _heartbeatTimer = new Timer(SendHeartbeats, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                
            ServerStarted?.Invoke(this, EventArgs.Empty);
                
            Console.WriteLine($"Server started on {IpAddress}:{Port}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start server: {ex.Message}", ex);
        }
    }
        
    private async Task AcceptClientsAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client), _cancellationTokenSource.Token);
            }
            catch (ObjectDisposedException)
            {
                // 监听器已关闭
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }
        
    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        string clientId = Guid.NewGuid().ToString();
        var clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            
        var session = new ClientSession
        {
            Id = clientId,
            TcpClient = tcpClient,
            IpAddress = clientIp,
            ConnectedTime = DateTime.Now,
            LastActivity = DateTime.Now
        };
            
        if (!_clients.TryAdd(clientId, session))
        {
            tcpClient.Close();
            return;
        }
            
        var clientInfo = new ClientInfo
        {
            Id = clientId,
            IpAddress = clientIp,
            ConnectedTime = session.ConnectedTime,
            LastActivity = session.LastActivity
        };
            
        ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientInfo));
        Console.WriteLine($"Client connected: {clientId} ({clientIp})");
            
        try
        {
            // 发送元数据给客户端
            await SendMetadataAsync(session);
                
            var buffer = new byte[4096];
            var stream = tcpClient.GetStream();
                
            while (!_cancellationTokenSource.Token.IsCancellationRequested && tcpClient.Connected)
            {
                // 读取消息长度头
                var headerBuffer = new byte[9]; // 类型(1) + 序列号(4) + 数据长度(4)
                int headerBytesRead = await stream.ReadAsync(headerBuffer, 0, 9, _cancellationTokenSource.Token);
                    
                if (headerBytesRead == 0)
                {
                    // 连接已关闭
                    break;
                }
                    
                if (headerBytesRead < 9)
                {
                    // 不完整的消息头
                    await Task.Delay(10);
                    continue;
                }
                    
                using var headerMs = new MemoryStream(headerBuffer);
                using var headerReader = new BinaryReader(headerMs, Encoding.UTF8);
                    
                var messageType = (MessageType)headerReader.ReadByte();
                int sequenceNumber = headerReader.ReadInt32();
                int dataLength = headerReader.ReadInt32();
                    
                // 读取消息体
                if (dataLength > 0)
                {
                    byte[] dataBuffer = new byte[dataLength];
                    int totalRead = 0;
                        
                    while (totalRead < dataLength)
                    {
                        int bytesRead = await stream.ReadAsync(dataBuffer, totalRead, dataLength - totalRead, 
                            _cancellationTokenSource.Token);
                            
                        if (bytesRead == 0)
                        {
                            // 连接已关闭
                            break;
                        }
                            
                        totalRead += bytesRead;
                    }
                        
                    // 处理消息
                    await ProcessClientMessage(session, messageType, dataBuffer, sequenceNumber);
                }
                    
                session.LastActivity = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {clientId}: {ex.Message}");
        }
        finally
        {
            DisconnectClient(clientId, "Connection closed");
        }
    }
        
    private async Task ProcessClientMessage(ClientSession session, MessageType messageType, byte[] data, int sequenceNumber)
    {
        try
        {
            switch (messageType)
            {
                case MessageType.Control:
                    // 处理客户端控制命令
                    await HandleControlMessage(session, data);
                    break;
                        
                case MessageType.Heartbeat:
                    // 更新最后活动时间
                    session.LastActivity = DateTime.Now;
                    break;
                        
                case MessageType.Metadata:
                    // 发送元数据
                    await SendMetadataAsync(session);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing client message: {ex.Message}");
        }
    }
        
    private async Task HandleControlMessage(ClientSession session, byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms, Encoding.UTF8);
            
        var command = (ControlCommand)reader.ReadByte();
        float parameter = reader.ReadSingle();
            
        Console.WriteLine($"Received control command from {session.Id}: {command} ({parameter})");
            
        // 这里可以处理特定的控制命令
        switch (command)
        {
            case ControlCommand.RequestMetadata:
                await SendMetadataAsync(session);
                break;
        }
    }
        
    private async Task SendMetadataAsync(ClientSession session)
    {
        try
        {
            var message = ProtocolMessage.CreateMetadataMessage(Metadata);
            await SendMessageAsync(session, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending metadata to {session.Id}: {ex.Message}");
        }
    }
        
    public void BroadcastAudioData(byte[] audioData)
    {
        if (!IsRunning || audioData == null || audioData.Length == 0)
            return;
            
        var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
        var message = ProtocolMessage.CreateAudioMessage(audioData, sequenceNumber);
            
        // 并行发送给所有客户端
        var tasks = _clients.Values
            .Where(client => client.TcpClient.Connected)
            .Select(client => SendMessageAsync(client, message))
            .ToList();
            
        // 不等待所有任务完成，避免阻塞
        Task.WhenAll(tasks).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                foreach (var ex in t.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())
                {
                    Console.WriteLine($"Error broadcasting audio: {ex.Message}");
                }
            }
        });
    }
        
    public void BroadcastControlCommand(ControlCommand command, float parameter = 0)
    {
        if (!IsRunning)
            return;
            
        var message = ProtocolMessage.CreateControlMessage(command, parameter);
            
        foreach (var client in _clients.Values.Where(c => c.TcpClient.Connected))
        {
            _ = SendMessageAsync(client, message);
        }
    }
        
    private async Task SendMessageAsync(ClientSession session, ProtocolMessage message)
    {
        if (!session.TcpClient.Connected)
            return;
            
        try
        {
            var data = message.Serialize();
            var stream = session.TcpClient.GetStream();
                
            await stream.WriteAsync(data, 0, data.Length);
            session.LastActivity = DateTime.Now;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to {session.Id}: {ex.Message}");
            DisconnectClient(session.Id, "Send error");
        }
    }
        
    private void SendHeartbeats(object state)
    {
        if (!IsRunning)
            return;
            
        var message = ProtocolMessage.CreateHeartbeatMessage();
            
        foreach (var client in _clients.Values.Where(c => c.TcpClient.Connected))
        {
            _ = SendMessageAsync(client, message);
        }
            
        // 清理不活动的客户端
        CleanupInactiveClients();
    }
        
    private void CleanupInactiveClients()
    {
        var cutoffTime = DateTime.Now.AddMinutes(-5); // 5分钟无活动
            
        foreach (var client in _clients.Values.Where(c => c.LastActivity < cutoffTime))
        {
            DisconnectClient(client.Id, "Inactive timeout");
        }
    }
        
    public void DisconnectClient(string clientId)
    {
        DisconnectClient(clientId, "Requested by server");
    }
        
    private void DisconnectClient(string clientId, string reason)
    {
        if (_clients.TryRemove(clientId, out var session))
        {
            try
            {
                session.TcpClient?.Close();
                    
                var clientInfo = new ClientInfo
                {
                    Id = session.Id,
                    IpAddress = session.IpAddress,
                    ConnectedTime = session.ConnectedTime,
                    LastActivity = session.LastActivity
                };
                    
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientInfo, reason));
                Console.WriteLine($"Client disconnected: {clientId} - {reason}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting client {clientId}: {ex.Message}");
            }
        }
    }
        
    public void DisconnectAllClients()
    {
        foreach (var clientId in _clients.Keys.ToList())
        {
            DisconnectClient(clientId, "Server shutdown");
        }
    }
        
    public IReadOnlyList<ClientInfo> GetClients()
    {
        return _clients.Values.Select(session => new ClientInfo
        {
            Id = session.Id,
            IpAddress = session.IpAddress,
            ConnectedTime = session.ConnectedTime,
            LastActivity = session.LastActivity
        }).ToList();
    }
        
    public void Stop()
    {
        if (!IsRunning)
            return;
            
        try
        {
            _cancellationTokenSource?.Cancel();
                
            // 断开所有客户端
            DisconnectAllClients();
                
            // 停止监听
            _listener?.Stop();
                
            // 停止心跳定时器
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
                
            // 等待接受任务完成
            _acceptTask?.Wait(TimeSpan.FromSeconds(5));
                
            IsRunning = false;
            ServerStopped?.Invoke(this, EventArgs.Empty);
                
            Console.WriteLine("Server stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
        }
    }
        
    private class ClientSession
    {
        public string Id { get; set; }
        public TcpClient TcpClient { get; set; }
        public string IpAddress { get; set; }
        public DateTime ConnectedTime { get; set; }
        public DateTime LastActivity { get; set; }
    }
        
    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
        _listener?.Server?.Dispose();
    }
}