using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频播放器
/// </summary>
public class AudioPlayer : IDisposable
{
    private IWavePlayer _waveOut;
    private BufferedWaveProvider _waveProvider;
    private WaveFormat _waveFormat;
    private bool _isPlaying;
        
    public bool IsPlaying => _isPlaying;
    public WaveFormat WaveFormat => _waveFormat;
        
    /// <summary>
    /// 初始化音频播放器
    /// </summary>
    /// <param name="sampleRate">采样率</param>
    /// <param name="channels">声道数</param>
    /// <param name="bitsPerSample">位深度</param>
    public AudioPlayer(int sampleRate = 44100, int channels = 2, int bitsPerSample = 16)
    {
        _waveFormat = new WaveFormat(sampleRate, channels, bitsPerSample);
        Initialize();
    }
        
    /// <summary>
    /// 使用波形格式初始化
    /// </summary>
    /// <param name="waveFormat">波形格式</param>
    public AudioPlayer(WaveFormat waveFormat)
    {
        _waveFormat = waveFormat;
        Initialize();
    }
        
    private void Initialize()
    {
        _waveProvider = new BufferedWaveProvider(_waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(2), // 2秒缓冲区
            DiscardOnBufferOverflow = true
        };
            
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
    }
        
    /// <summary>
    /// 开始播放
    /// </summary>
    public void Start()
    {
        if (_isPlaying)
            return;
            
        _waveOut.Play();
        _isPlaying = true;
        Console.WriteLine("Audio player started");
    }
        
    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        if (!_isPlaying)
            return;
            
        _waveOut.Stop();
        _isPlaying = false;
        Console.WriteLine("Audio player stopped");
    }
        
    /// <summary>
    /// 添加音频数据到播放缓冲区
    /// </summary>
    /// <param name="audioData">音频数据</param>
    public void AddAudioData(byte[] audioData)
    {
        if (audioData == null || audioData.Length == 0)
            return;
            
        if (_waveProvider != null)
        {
            _waveProvider.AddSamples(audioData, 0, audioData.Length);
        }
    }
        
    /// <summary>
    /// 清除缓冲区
    /// </summary>
    public void ClearBuffer()
    {
        if (_waveProvider != null)
        {
            _waveProvider.ClearBuffer();
        }
    }
        
    /// <summary>
    /// 获取缓冲区剩余时长（秒）
    /// </summary>
    public double GetBufferDuration()
    {
        if (_waveProvider == null)
            return 0;
            
        int bufferedBytes = _waveProvider.BufferedBytes;
        int bytesPerSecond = _waveFormat.AverageBytesPerSecond;
            
        return (double)bufferedBytes / bytesPerSecond;
    }
        
    /// <summary>
    /// 等待缓冲区清空
    /// </summary>
    public void WaitForBufferToEmpty(int timeoutMilliseconds = 5000)
    {
        DateTime startTime = DateTime.Now;
            
        while (GetBufferDuration() > 0.1) // 等待缓冲区小于100ms
        {
            if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
            {
                Console.WriteLine("Timeout waiting for buffer to empty");
                break;
            }
                
            Thread.Sleep(100);
        }
    }
        
    public void Dispose()
    {
        Stop();
            
        _waveOut?.Dispose();
        _waveOut = null;
            
        _waveProvider = null;
            
        Console.WriteLine("Audio player disposed");
    }
}