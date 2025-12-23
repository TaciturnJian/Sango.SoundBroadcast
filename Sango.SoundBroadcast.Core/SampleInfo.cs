using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

public record SampleInfo(ISampleProvider Provider, float Gain = 1.0f);