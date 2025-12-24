using NAudio.Wave;
using System.Runtime.InteropServices;

namespace Sango.SoundBroadcast.Core;

[StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct SoundPackageHeader
{
    public DateTime Timestamp;
    public int SampleRate;
    public int SampleBits;
    public int Channels;
    public int DataLength; // 应当小于 1024

    public static int GetSize()
    {
        return Marshal.SizeOf(typeof(SoundPackageHeader));
    }

    public static SoundPackageHeader? FromBytes(byte[] data)
    {
        if (data.Length < GetSize())
            return null;


        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            return Marshal.PtrToStructure<SoundPackageHeader>(ptr);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HeartbeatMessage.FromBytes 转换字节时出现错误：{ex}");
            return null;
        }
        finally
        {
            handle.Free();
        }
    }

    public byte[] ToBytes()
    {
        var type = typeof(SoundPackageHeader);
        var size = Marshal.SizeOf(type);
        var data = new byte[size];
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(this, ptr, true);
        Marshal.Copy(ptr, data, 0, size);
        Marshal.FreeHGlobal(ptr);
        return data;
    }

    public void ReadFrom(WaveFormat format)
    {
        SampleRate = format.SampleRate;
        SampleBits = format.BitsPerSample;
        Channels = format.Channels;
    }

    public WaveFormat ToFormat()
    {
        return new WaveFormat(SampleRate, SampleBits, Channels);
    }
}