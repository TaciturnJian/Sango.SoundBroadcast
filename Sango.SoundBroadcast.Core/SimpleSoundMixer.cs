using System.Collections.Concurrent;

namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 简单音频混合器
/// </summary>
public class SimpleSoundMixer : ISoundMixer
{
    private const float DEFAULT_MAXIMUM_GAIN = 3.0f;
        
    private readonly ConcurrentDictionary<string, ProviderInfo> _providers;
    private readonly ConcurrentQueue<byte[]> _audioBufferQueue;
    private readonly object _mixLock = new object();
    private readonly Timer _mixTimer;
    private bool _isInitialized = false;
    private bool _isRunning = false;
    private int _bytesProcessed = 0;
    private int _mixedSamples = 0;
    private DateTime _lastMixTime;
        
    public event EventHandler<AudioDataAvailableEventArgs> AudioDataAvailable;
        
    public string Name { get; set; }
    public float MaximumGain { get; private set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public int BitsPerSample { get; set; }
    public bool IsRunning => _isRunning;
        
    public SimpleSoundMixer(string name = "Sound Mixer")
    {
        Name = name;
        MaximumGain = DEFAULT_MAXIMUM_GAIN;
            
        // 默认输出格式：CD音质
        SampleRate = 44100;
        Channels = 2;
        BitsPerSample = 16;
            
        _providers = new ConcurrentDictionary<string, ProviderInfo>();
        _audioBufferQueue = new ConcurrentQueue<byte[]>();
            
        // 创建混合定时器（每20ms执行一次）
        _mixTimer = new Timer(MixAudioCallback, null, Timeout.Infinite, Timeout.Infinite);
    }
        
    public void Initialize()
    {
        if (_isInitialized)
            return;
            
        _isInitialized = true;
        Console.WriteLine($"Mixer initialized: {Name}");
    }
        
    public void Start()
    {
        if (!_isInitialized)
            Initialize();
            
        if (_isRunning)
            return;
            
        _isRunning = true;
            
        // 启动所有已连接的提供者
        foreach (var providerInfo in _providers.Values)
        {
            if (providerInfo.Enabled)
            {
                providerInfo.Provider.Start();
            }
        }
            
        // 启动混合定时器
        _mixTimer.Change(0, 20); // 每20ms混合一次（50Hz）
            
        Console.WriteLine($"Mixer started: {Name}");
    }
        
    public void Stop()
    {
        if (!_isRunning)
            return;
            
        _isRunning = false;
            
        // 停止混合定时器
        _mixTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
        // 停止所有提供者
        foreach (var providerInfo in _providers.Values)
        {
            providerInfo.Provider.Stop();
        }
            
        // 清空缓冲区
        while (_audioBufferQueue.TryDequeue(out _)) { }
            
        Console.WriteLine($"Mixer stopped: {Name}");
    }
        
    public string AddProvider(ISoundProvider provider, string name = null, float initialGain = 1.0f)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
            
        initialGain = Math.Max(0.0f, Math.Min(MaximumGain, initialGain));
            
        string providerId = Guid.NewGuid().ToString();
        string displayName = name ?? provider.Name;
            
        var providerInfo = new ProviderInfo
        {
            Id = providerId,
            Name = displayName,
            Provider = provider,
            Gain = initialGain,
            Enabled = true,
            AddedTime = DateTime.Now,
            BytesProcessed = 0
        };
            
        // 订阅提供者的音频数据事件
        provider.AudioDataAvailable += (sender, e) =>
        {
            OnProviderAudioDataAvailable(providerId, e);
        };
            
        if (!_providers.TryAdd(providerId, providerInfo))
        {
            throw new InvalidOperationException("Failed to add provider");
        }
            
        // 如果混合器正在运行，启动提供者
        if (_isRunning && providerInfo.Enabled)
        {
            provider.Initialize();
            provider.Start();
        }
            
        Console.WriteLine($"Added provider: {displayName} (Gain: {initialGain})");
        return providerId;
    }
        
    public bool RemoveProvider(string providerId)
    {
        if (_providers.TryRemove(providerId, out var providerInfo))
        {
            if (providerInfo.Provider.IsRunning)
            {
                providerInfo.Provider.Stop();
            }
                
            providerInfo.Provider.Dispose();
            Console.WriteLine($"Removed provider: {providerInfo.Name}");
            return true;
        }
            
        return false;
    }
        
    public IReadOnlyList<ProviderInfo> GetProviders()
    {
        return _providers.Values.ToList();
    }
        
    public void SetProviderGain(string providerId, float gain)
    {
        if (_providers.TryGetValue(providerId, out var providerInfo))
        {
            gain = Math.Max(0.0f, Math.Min(MaximumGain, gain));
            providerInfo.Gain = gain;
            Console.WriteLine($"Set gain for {providerInfo.Name}: {gain}");
        }
    }
        
    public float GetProviderGain(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var providerInfo))
        {
            return providerInfo.Gain;
        }
            
        return 0.0f;
    }
        
    public void SetProviderEnabled(string providerId, bool enabled)
    {
        if (_providers.TryGetValue(providerId, out var providerInfo))
        {
            providerInfo.Enabled = enabled;
                
            if (_isRunning)
            {
                if (enabled)
                {
                    providerInfo.Provider.Start();
                }
                else
                {
                    providerInfo.Provider.Stop();
                }
            }
                
            Console.WriteLine($"Set enabled for {providerInfo.Name}: {enabled}");
        }
    }
        
    public bool GetProviderEnabled(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var providerInfo))
        {
            return providerInfo.Enabled;
        }
            
        return false;
    }
        
    public void SetOutputFormat(int sampleRate, int channels, int bitsPerSample)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot change format while mixer is running");
            
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
            
        Console.WriteLine($"Mixer output format set: {sampleRate}Hz, {channels}ch, {bitsPerSample}bit");
    }
        
    public void ClearProviders()
    {
        var providerIds = _providers.Keys.ToList();
            
        foreach (var providerId in providerIds)
        {
            RemoveProvider(providerId);
        }
            
        Console.WriteLine("All providers cleared");
    }
        
    public MixerStatistics GetStatistics()
    {
        int activeProviders = _providers.Values.Count(p => p.Enabled);
        float totalGain = _providers.Values.Where(p => p.Enabled).Sum(p => p.Gain);
        float averageGain = activeProviders > 0 ? totalGain / activeProviders : 0;
            
        return new MixerStatistics
        {
            TotalProviders = _providers.Count,
            ActiveProviders = activeProviders,
            TotalBytesProcessed = _bytesProcessed,
            MixedSamples = _mixedSamples,
            AverageGain = averageGain,
            LastMixTime = _lastMixTime
        };
    }
        
    private void OnProviderAudioDataAvailable(string providerId, AudioDataAvailableEventArgs e)
    {
        if (!_providers.TryGetValue(providerId, out var providerInfo))
            return;
            
        if (!providerInfo.Enabled || providerInfo.Gain <= 0.0f)
            return;
            
        // 更新处理字节数
        providerInfo.BytesProcessed += e.Length;
            
        // 将音频数据放入缓冲区队列
        _audioBufferQueue.Enqueue(e.AudioData);
    }
        
    private void MixAudioCallback(object state)
    {
        if (!_isRunning)
            return;
            
        lock (_mixLock)
        {
            try
            {
                // 获取所有缓冲区
                var buffers = new List<byte[]>();
                var gains = new List<float>();
                    
                while (_audioBufferQueue.TryDequeue(out var buffer))
                {
                    buffers.Add(buffer);
                    gains.Add(1.0f); // 这里假设所有缓冲区来自同一个提供者
                    // 实际实现中需要根据提供者ID来设置增益
                }
                    
                if (buffers.Count > 0)
                {
                    // 混合音频数据
                    var mixedAudio = AudioUtils.MixBuffers(buffers, gains, BitsPerSample, 1.0f);
                        
                    if (mixedAudio != null && mixedAudio.Length > 0)
                    {
                        _bytesProcessed += mixedAudio.Length;
                        _mixedSamples += mixedAudio.Length / (BitsPerSample / 8);
                        _lastMixTime = DateTime.Now;
                            
                        // 触发音频数据可用事件
                        AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(mixedAudio, mixedAudio.Length));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mixing audio: {ex.Message}");
            }
        }
    }
        
    public void Dispose()
    {
        Stop();
        _mixTimer?.Dispose();
            
        // 清理所有提供者
        ClearProviders();
    }
}