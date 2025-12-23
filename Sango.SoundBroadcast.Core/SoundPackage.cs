using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

public class SoundPackage
{
    public SoundPackageHeader Header;
    public byte[] Body = [];

    public void FromProvider(IWaveProvider provider)
    {
        Header.ReadFrom(provider.WaveFormat);
        Body = new byte[1024];
        var read_count = provider.Read(Body, 0, Body.Length);
        Header.DataLength = read_count;
    }
}