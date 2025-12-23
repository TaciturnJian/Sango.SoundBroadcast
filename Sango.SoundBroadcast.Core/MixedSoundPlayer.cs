using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

public class MixedSoundPlayer : IDisposable
{
    public SimpleAudioMixer Mixer { get; }
    public SimpleSoundPlayer Player { get; }

    public MixedSoundPlayer(WaveFormat format, CancellationTokenSource cts)
    {
        Player = new SimpleSoundPlayer(format);
        Mixer = new SimpleAudioMixer();
        Mixer.BeginMixingTo(Player.WaveProvider, cts);
    }

    public void Dispose()
    {
        Player.Dispose();
    }
}
