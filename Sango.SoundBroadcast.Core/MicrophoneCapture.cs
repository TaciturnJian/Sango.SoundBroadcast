using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 麦克风音频捕获提供者
/// </summary>
public class MicrophoneCapture : ISoundProvider
{
    private WasapiCapture? _waveIn;
    private BufferedWaveProvider _bufferedWaveProvider;
    private readonly MMDevice _captureDevice;
    private readonly string? _deviceId;
    private bool _isInitialized = false;
    
    public event EventHandler<AudioDataAvailableEventArgs> AudioDataAvailable;
    
    public int SampleRate { get; private set; } = 44100;
    public int Channels { get; private set; } = 2;
    public int BitsPerSample { get; private set; } = 16;
    public string Name => $"Microphone: {_captureDevice?.FriendlyName ?? _deviceId}";
    public bool IsRunning => _waveIn?.CaptureState == CaptureState.Capturing;
    
    /// <summary>
    /// 使用默认麦克风创建麦克风捕获
    /// </summary>
    public MicrophoneCapture() : this(null)
    {
    }
    
    /// <summary>
    /// 使用指定设备ID创建麦克风捕获
    /// </summary>
    /// <param name="deviceId">音频设备ID，为null时使用默认设备</param>
    public MicrophoneCapture(string? deviceId)
    {
        _deviceId = deviceId;
        
        var device_enumerator = new MMDeviceEnumerator();
        _captureDevice = deviceId is null 
            ? device_enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
            : device_enumerator.GetDevice(deviceId);
    }
    
    public void Initialize()
    {
        if (_isInitialized)
            return;
        
        try
        {
            var wave_format = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _waveIn = new WasapiCapture(_captureDevice, false, 100);
            _waveIn.WaveFormat = wave_format;
            
            _bufferedWaveProvider = new BufferedWaveProvider(wave_format)
            {
                BufferDuration = TimeSpan.FromSeconds(1),
                DiscardOnBufferOverflow = true
            };
            
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize microphone capture: {ex.Message}", ex);
        }
    }
    
    public void Start()
    {
        if (!_isInitialized)
            Initialize();
        
        if (_waveIn == null)
            throw new InvalidOperationException("Microphone not initialized");
        
        if (_waveIn.CaptureState != CaptureState.Capturing)
        {
            _waveIn.StartRecording();
        }
    }
    
    public void Stop()
    {
        if (_waveIn != null && _waveIn.CaptureState == CaptureState.Capturing)
        {
            _waveIn.StopRecording();
        }
    }
    
    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        var audio_data = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, audio_data, 0, e.BytesRecorded);
        
        AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(audio_data, e.BytesRecorded));
    }
    
    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            // 处理异常
            Console.WriteLine($"Microphone capture error: {e.Exception.Message}");
        }
    }
    
    /// <summary>
    /// 获取所有可用的麦克风设备
    /// </summary>
    public static List<MMDevice> GetAvailableMicrophones()
    {
        var deviceEnumerator = new MMDeviceEnumerator();
        return deviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .ToList();
    }
    
    /// <summary>
    /// 设置音频格式
    /// </summary>
    public void SetAudioFormat(int sampleRate = 44100, int channels = 2, int bitsPerSample = 16)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Cannot change format after initialization");
        
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
    }
    
    public void Dispose()
    {
        Stop();
        
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }
        
        _captureDevice?.Dispose();
    }
}