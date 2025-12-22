namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频提供者接口，用于从不同源获取音频数据
/// </summary>
public interface ISoundProvider : IDisposable
{
    /// <summary>
    /// 音频数据可用事件
    /// </summary>
    event EventHandler<AudioDataAvailableEventArgs> AudioDataAvailable;
        
    /// <summary>
    /// 采样率（Hz）
    /// </summary>
    int SampleRate { get; }
        
    /// <summary>
    /// 声道数
    /// </summary>
    int Channels { get; }
        
    /// <summary>
    /// 位深度（通常为16或32）
    /// </summary>
    int BitsPerSample { get; }
        
    /// <summary>
    /// 音频提供者名称
    /// </summary>
    string Name { get; }
        
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }
        
    /// <summary>
    /// 初始化音频提供者
    /// </summary>
    void Initialize();
        
    /// <summary>
    /// 开始捕获音频
    /// </summary>
    void Start();
        
    /// <summary>
    /// 停止捕获音频
    /// </summary>
    void Stop();
}