using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音乐文件音频提供者
/// </summary>
public class FileSoundProvider : ISoundProvider
{
    private AudioFileReader _audioFileReader;
    private WaveChannel32 _waveChannel;
    private BufferedWaveProvider _bufferedWaveProvider;
    private Thread _playbackThread;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly string _filePath;
    private bool _loop = false;
    private float _volume = 1.0f;
        
    public event EventHandler<AudioDataAvailableEventArgs> AudioDataAvailable;
        
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public int BitsPerSample { get; private set; }
    public string Name => $"File: {Path.GetFileName(_filePath)}";
    public bool IsRunning => _playbackThread?.IsAlive == true;
        
    /// <summary>
    /// 是否循环播放
    /// </summary>
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }
        
    /// <summary>
    /// 音量 (0.0 - 1.0)
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Max(0.0f, Math.Min(1.0f, value));
            if (_waveChannel != null)
            {
                _waveChannel.Volume = _volume;
            }
        }
    }
        
    /// <summary>
    /// 创建文件音频提供者
    /// </summary>
    /// <param name="filePath">音频文件路径</param>
    public FileSoundProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));
            
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}");
            
        _filePath = filePath;
    }
        
    public void Initialize()
    {
        if (_audioFileReader != null)
            return;
            
        try
        {
            // 打开音频文件
            _audioFileReader = new AudioFileReader(_filePath);
                
            // 创建音量控制器
            _waveChannel = new WaveChannel32(_audioFileReader);
            _waveChannel.Volume = _volume;
                
            // 设置音频格式属性
            var waveFormat = _waveChannel.WaveFormat;
            SampleRate = waveFormat.SampleRate;
            Channels = waveFormat.Channels;
            BitsPerSample = waveFormat.BitsPerSample;
                
            // 创建缓冲提供者
            _bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(0.5),
                DiscardOnBufferOverflow = true
            };
                
            // 创建读取缓冲区
            int bufferSize = waveFormat.AverageBytesPerSecond / 10; // 100ms 缓冲区
            _cancellationTokenSource = new CancellationTokenSource();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize file sound provider: {ex.Message}", ex);
        }
    }
        
    public void Start()
    {
        if (_audioFileReader == null)
            Initialize();
            
        if (_playbackThread == null || !_playbackThread.IsAlive)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _playbackThread = new Thread(PlaybackWorker);
            _playbackThread.IsBackground = true;
            _playbackThread.Start();
        }
    }
        
    public void Stop()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
            
        if (_playbackThread != null)
        {
            _playbackThread.Join(1000);
            _playbackThread = null;
        }
    }
        
    private void PlaybackWorker()
    {
        try
        {
            int bufferSize = _audioFileReader.WaveFormat.AverageBytesPerSecond / 10; // 100ms 缓冲区
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
                
            do
            {
                // 重置到文件开始（如果是循环播放）
                if (_audioFileReader.Position >= _audioFileReader.Length)
                {
                    if (_loop)
                    {
                        _audioFileReader.Position = 0;
                    }
                    else
                    {
                        break;
                    }
                }
                    
                // 读取音频数据
                bytesRead = _waveChannel.Read(buffer, 0, buffer.Length);
                    
                if (bytesRead > 0)
                {
                    var audioData = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, audioData, 0, bytesRead);
                        
                    // 触发音频数据可用事件
                    AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(audioData, bytesRead));
                        
                    // 控制播放速度，模拟实时播放
                    int sleepTime = (bytesRead * 1000) / _audioFileReader.WaveFormat.AverageBytesPerSecond;
                    Thread.Sleep(sleepTime);
                }
                    
            } while (bytesRead > 0 && !_cancellationTokenSource.Token.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"File playback error: {ex.Message}");
        }
    }
        
    /// <summary>
    /// 跳转到指定位置
    /// </summary>
    /// <param name="position">位置（秒）</param>
    public void Seek(double position)
    {
        if (_audioFileReader != null)
        {
            _audioFileReader.CurrentTime = TimeSpan.FromSeconds(position);
        }
    }
        
    /// <summary>
    /// 获取音频时长（秒）
    /// </summary>
    public double GetDuration()
    {
        return _audioFileReader?.TotalTime.TotalSeconds ?? 0;
    }
        
    /// <summary>
    /// 获取当前播放位置（秒）
    /// </summary>
    public double GetCurrentPosition()
    {
        return _audioFileReader?.CurrentTime.TotalSeconds ?? 0;
    }
        
    public void Dispose()
    {
        Stop();
            
        _waveChannel?.Dispose();
        _waveChannel = null;
            
        _audioFileReader?.Dispose();
        _audioFileReader = null;
            
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
