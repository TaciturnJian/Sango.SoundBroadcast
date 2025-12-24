using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Sango.SoundBroadcast.Core;

public class SoundServer(WaveFormat format, CancellationTokenSource cts) : IDisposable
{
    private readonly ConcurrentDictionary<string, ClientInfo> Clients = new();

    public string Name { get; set; } = "anonymous";

    public MixedSoundPlayer Player { get; set; } = new (format, cts);

    private void HandleHeartbeatMessage(Socket socket, EndPoint remote, byte[] data)
    {
        Console.WriteLine($"收到来自({remote.Serialize()})的心跳包({data.Length}字节)，正在处理");
        try
        {
            var message = HeartbeatMessage.FromBytes(data);
            if (message is null) return;

            var client_info = new ClientInfo(remote, message.Value.Name, message.Value.TimeStamp);
            if (Clients.TryGetValue(client_info.Name, out var old_info) && old_info.LastHeartbeat > client_info.LastHeartbeat) 
                return;
            Clients.AddOrUpdate(client_info.Name, client_info, (_,_) => client_info);
            
            Console.WriteLine($"客户端[{client_info.Name}]已注册/更新，当前在线客户端数：{Clients.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理心跳消息失败：{ex.Message}");
        }
    }

    private void HandleSoundPackage(Socket socket, EndPoint remote, byte[] data)
    {
        Console.WriteLine($"收到来自({remote.Serialize()})的音频包({data.Length}字节)，正在处理");
        try
        {
            var header = SoundPackageHeader.FromBytes(data);
            if (header is null || header.Value.DataLength <= 0) return;

            var header_size = SoundPackageHeader.GetSize();
            using var input = new MemoryStream(data, header_size, data.Length - header_size);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(output);
            }
            var buffer = output.ToArray();

            var waveform = header.Value.ToFormat();
            var provider = new BufferedWaveProvider(waveform)
            {
                BufferDuration = TimeSpan.FromSeconds(3),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };
            provider.AddSamples(buffer, 0, buffer.Length);
            var id = Player.Mixer.AddSample(new SampleInfo(provider.ToSampleProvider()));
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Player.Mixer.RemoveSample(id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理音频包失败：{ex.Message}");
        }
    }

    public void HandleClientMessage(Socket socket, EndPoint remote, byte[] data)
    {
        if (data.Length <= SoundPackageHeader.GetSize()) HandleHeartbeatMessage(socket, remote, data);
        else HandleSoundPackage(socket, remote, data);
    }

    public void SendHeartbeatMessage(Socket socket, EndPoint remote)
    {
        var name = Name.Length > 7 ? Name[..7] : Name;
        var message = new HeartbeatMessage()
        {
            Name = name,
            TimeStamp = DateTime.UtcNow,
        };
        try
        {
            var bytes = message.ToBytes();
            var send_bytes = socket.SendTo(bytes, remote);
            if (send_bytes < bytes.Length)
            {
                throw new Exception($"发送的数据包不完整：({send_bytes} < {bytes.Length})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发送数据到远程服务器时失败： {ex}");
        }
    }

    public void SendMessage(Socket socket, byte[] data)
    {
        if (data.Length == 0) return;
        
        foreach (var _ in Clients.Keys)
        {
            if (!Clients.TryGetValue(_, out var info)) continue;
            try
            {
                socket.SendTo(data, info.Remote);
                info.Attenuation--;
                if (info.Attenuation <= 0)
                {
                    Console.WriteLine(Clients.TryRemove(info.Name, out var _)
                        ? $"客户端[{info.Name}]衰减值为0，已移除"
                        : $"[警告] 客户端[{info.Name}]衰减值为0，但是移除失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"向客户端[{info.Name}]广播数据失败：{ex.Message}");
                Clients.TryRemove(info.Name, out var _);
            }
        }
    }

    public void Dispose()
    {
        Player.Dispose();
        Clients.Clear();
    }
}

