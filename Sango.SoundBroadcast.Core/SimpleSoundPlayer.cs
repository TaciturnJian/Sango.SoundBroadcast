using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

public class SimpleSoundPlayer : IDisposable
{
    public BufferedWaveProvider WaveProvider { get; private set; }

    public WaveOutEvent OutEvent { get; private set; }

    public SimpleSoundPlayer(WaveFormat format)
    {
        WaveProvider = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true
        };

        OutEvent = new WaveOutEvent();
        OutEvent.Init(WaveProvider);
        OutEvent.Play();
    }

    public void Dispose()
    {
        OutEvent.Stop();
        OutEvent.Dispose();
    }
}
