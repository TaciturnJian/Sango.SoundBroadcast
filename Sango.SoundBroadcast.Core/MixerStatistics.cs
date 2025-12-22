namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 混合器统计信息
/// </summary>
public class MixerStatistics
{
    public int TotalProviders { get; set; }
    public int ActiveProviders { get; set; }
    public int TotalBytesProcessed { get; set; }
    public int MixedSamples { get; set; }
    public double AverageGain { get; set; }
    public DateTime LastMixTime { get; set; }
}