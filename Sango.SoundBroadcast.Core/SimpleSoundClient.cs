using System.Net.Sockets;
using System.Text;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 简单的TCP音频客户端
/// </summary>
public class SimpleSoundClient : ISoundClient
{
    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _receiveTask;
    private Timer _heartbeatTimer;
    private readonly string _clientId;
        
    public event EventHandler Connected;
    public event EventHandler<DisconnectedEventArgs> Disconnected;
    public event EventHandler<AudioDataReceivedEventArgs> AudioDataReceived;
    public event EventHandler<MetadataReceivedEventArgs> MetadataReceived;
    public event EventHandler<ControlCommandReceivedEventArgs> ControlCommandReceived;
        
    public bool IsConnected { get; private set; }
    public string ServerAddress { get; }
    public int ServerPort { get; }
    public string ClientId => _clientId;
    public AudioMetadata Metadata { get; private set; }
        
    public SimpleSoundClient(string serverAddress, int serverPort)
    {
        ServerAddress = serverAddress ?? throw new ArgumentNullException(nameof(serverAddress));
        ServerPort = serverPort;
        _clientId = Guid.NewGuid().ToString().Substring(0, 8);
        Metadata = new AudioMetadata();
    }
        
    public void Connect()
    {
        if (IsConnected)
            return;
            
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
                
            // 连接服务器
            _tcpClient = new TcpClient();
            _tcpClient.Connect(ServerAddress, ServerPort);
            _networkStream = _tcpClient.GetStream();
                
            IsConnected = true;
                
            // 启动接收任务
            _receiveTask = Task.Run(ReceiveMessagesAsync, _cancellationTokenSource.Token);
                
            // 启动心跳定时器
            _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                
            Connected?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($"Connected to server {ServerAddress}:{ServerPort}");
                
            // 请求元数据
            RequestMetadata();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to server: {ex.Message}", ex);
        }
    }
        
    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];
            
        while (!_cancellationTokenSource.Token.IsCancellationRequested && _tcpClient.Connected)
        {
            try
            {
                // 读取消息长度头
                var headerBuffer = new byte[9]; // 类型(1) + 序列号(4) + 数据长度(4)
                int headerBytesRead = await _networkStream.ReadAsync(headerBuffer, 0, 9, _cancellationTokenSource.Token);
                    
                if (headerBytesRead == 0)
                {
                    // 连接已关闭
                    Disconnect("Connection closed by server");
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
                        int bytesRead = await _networkStream.ReadAsync(dataBuffer, totalRead, dataLength - totalRead, 
                            _cancellationTokenSource.Token);
                            
                        if (bytesRead == 0)
                        {
                            // 连接已关闭
                            Disconnect("Connection closed while reading data");
                            break;
                        }
                            
                        totalRead += bytesRead;
                    }
                        
                    // 处理消息
                    ProcessMessage(messageType, dataBuffer, sequenceNumber);
                }
                else
                {
                    // 无数据的消息（如心跳包）
                    ProcessMessage(messageType, Array.Empty<byte>(), sequenceNumber);
                }
            }
            catch (IOException)
            {
                // 连接断开
                Disconnect("Connection lost");
                break;
            }
            catch (ObjectDisposedException)
            {
                // 连接已释放
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                if (!_tcpClient.Connected)
                {
                    Disconnect("Connection error");
                    break;
                }
            }
        }
    }
        
    private void ProcessMessage(MessageType messageType, byte[] data, int sequenceNumber)
    {
        try
        {
            switch (messageType)
            {
                case MessageType.AudioData:
                    HandleAudioData(data, sequenceNumber);
                    break;
                        
                case MessageType.Metadata:
                    HandleMetadata(data);
                    break;
                        
                case MessageType.Control:
                    HandleControlCommand(data);
                    break;
                        
                case MessageType.Heartbeat:
                    // 心跳包，无需处理
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    }
        
    private void HandleAudioData(byte[] data, int sequenceNumber)
    {
        AudioDataReceived?.Invoke(this, 
            new AudioDataReceivedEventArgs(data, sequenceNumber, DateTime.Now));
            
        // 可以在这里添加音频数据缓冲或处理逻辑
    }
        
    private void HandleMetadata(byte[] data)
    {
        try
        {
            Metadata = AudioMetadata.Deserialize(data);
            MetadataReceived?.Invoke(this, new MetadataReceivedEventArgs(Metadata));
                
            Console.WriteLine($"Received metadata: {Metadata.ProviderName}, " +
                              $"{Metadata.SampleRate}Hz, {Metadata.Channels}ch, {Metadata.BitsPerSample}bit");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing metadata: {ex.Message}");
        }
    }
        
    private void HandleControlCommand(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms, Encoding.UTF8);
                
            var command = (ControlCommand)reader.ReadByte();
            float parameter = reader.ReadSingle();
                
            ControlCommandReceived?.Invoke(this, 
                new ControlCommandReceivedEventArgs(command, parameter));
                
            Console.WriteLine($"Received control command: {command} ({parameter})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing control command: {ex.Message}");
        }
    }
        
    private void SendHeartbeat(object state)
    {
        if (!IsConnected)
            return;
            
        try
        {
            var message = ProtocolMessage.CreateHeartbeatMessage();
            SendMessage(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending heartbeat: {ex.Message}");
        }
    }
        
    public void SendControlCommand(ControlCommand command, float parameter = 0)
    {
        if (!IsConnected)
            return;
            
        try
        {
            var message = ProtocolMessage.CreateControlMessage(command, parameter);
            SendMessage(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending control command: {ex.Message}");
        }
    }
        
    public void RequestMetadata()
    {
        SendControlCommand(ControlCommand.RequestMetadata);
    }
        
    private void SendMessage(ProtocolMessage message)
    {
        if (!IsConnected)
            return;
            
        try
        {
            var data = message.Serialize();
            _networkStream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
            Disconnect("Send error");
        }
    }
        
    public void Disconnect()
    {
        Disconnect("Requested by client");
    }
        
    private void Disconnect(string reason)
    {
        if (!IsConnected)
            return;
            
        try
        {
            IsConnected = false;
                
            _cancellationTokenSource?.Cancel();
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
                
            // 等待接收任务完成
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
                
            _networkStream?.Close();
            _tcpClient?.Close();
                
            Disconnected?.Invoke(this, new DisconnectedEventArgs(reason));
            Console.WriteLine($"Disconnected from server: {reason}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting: {ex.Message}");
        }
    }
        
    public void Dispose()
    {
        Disconnect();
        _cancellationTokenSource?.Dispose();
    }
}