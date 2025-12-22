namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频混合器接口
/// </summary>
public interface ISoundMixer : ISoundProvider
{
    /// <summary>
    /// 混合器名称
    /// </summary>
    new string Name { get; set; }
       
    /// <summary>
    /// 最大增益值
    /// </summary>
    float MaximumGain { get; }
       
    /// <summary>
    /// 输出采样率
    /// </summary>
    new int SampleRate { get; set; }
       
    /// <summary>
    /// 输出声道数
    /// </summary>
    new int Channels { get; set; }
       
    /// <summary>
    /// 输出位深度
    /// </summary>
    new int BitsPerSample { get; set; }
       
    /// <summary>
    /// 添加音频提供者
    /// </summary>
    /// <param name="provider">音频提供者</param>
    /// <param name="name">显示名称</param>
    /// <param name="initialGain">初始增益</param>
    /// <returns>提供者ID</returns>
    string AddProvider(ISoundProvider provider, string name = null, float initialGain = 1.0f);
       
    /// <summary>
    /// 移除音频提供者
    /// </summary>
    /// <param name="providerId">提供者ID</param>
    /// <returns>是否成功移除</returns>
    bool RemoveProvider(string providerId);
       
    /// <summary>
    /// 获取所有音频提供者信息
    /// </summary>
    IReadOnlyList<ProviderInfo> GetProviders();
       
    /// <summary>
    /// 设置提供者增益
    /// </summary>
    /// <param name="providerId">提供者ID</param>
    /// <param name="gain">增益值 (0.0 - MaximumGain)</param>
    void SetProviderGain(string providerId, float gain);
       
    /// <summary>
    /// 获取提供者增益
    /// </summary>
    /// <param name="providerId">提供者ID</param>
    /// <returns>增益值</returns>
    float GetProviderGain(string providerId);
       
    /// <summary>
    /// 启用/禁用提供者
    /// </summary>
    /// <param name="providerId">提供者ID</param>
    /// <param name="enabled">是否启用</param>
    void SetProviderEnabled(string providerId, bool enabled);
       
    /// <summary>
    /// 获取提供者启用状态
    /// </summary>
    /// <param name="providerId">提供者ID</param>
    /// <returns>是否启用</returns>
    bool GetProviderEnabled(string providerId);
       
    /// <summary>
    /// 设置混合器输出格式
    /// </summary>
    /// <param name="sampleRate">采样率</param>
    /// <param name="channels">声道数</param>
    /// <param name="bitsPerSample">位深度</param>
    void SetOutputFormat(int sampleRate, int channels, int bitsPerSample);
       
    /// <summary>
    /// 清空所有音频提供者
    /// </summary>
    void ClearProviders();
       
    /// <summary>
    /// 混合器统计信息
    /// </summary>
    MixerStatistics GetStatistics();
}