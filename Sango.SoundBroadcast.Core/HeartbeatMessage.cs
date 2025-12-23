using System.Runtime.InteropServices;

namespace Sango.SoundBroadcast.Core;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct HeartbeatMessage
{
    public DateTime TimeStamp;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Name;

    public static HeartbeatMessage? FromBytes(byte[] data)
    {
        if (data.Length < Marshal.SizeOf(typeof(HeartbeatMessage)))
            return null;
        

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var ptr = handle.AddrOfPinnedObject();
            return Marshal.PtrToStructure<HeartbeatMessage>(ptr);
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
        var type = typeof(HeartbeatMessage);
        var size = Marshal.SizeOf(type);
        var data = new byte[size];
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(this, ptr, true);
        Marshal.Copy(ptr, data, 0, size);
        Marshal.FreeHGlobal(ptr);
        return data;
    }
}