using System.Collections.Concurrent;
using NAudio.Wave;

using Sango.SoundBroadcast.Core;

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

if (args.Length < 2)
{
    Console.WriteLine("用法：\n\t<程序名> <用户名> <监听的IP地址:端口>");
    return;
}

var user_name = args[0];
var listen_ep_str = args[1];

if (!IPEndPoint.TryParse(listen_ep_str, out var host_ep))
{
    Console.WriteLine("无法解析要监听的地址或端口");
    return;
}

const int PAYLOAD_SIZE = 512;

Console.WriteLine($"正在尝试作为用户({user_name})运行在({listen_ep_str})上的服务");

var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

try
{
    socket.Bind(host_ep);
}
catch (Exception ex)
{
    Console.WriteLine($"绑定到目标端口失败：{ex}");
    return;
}

const string FILE_PATH = "./music.mp3";

using var cts = new CancellationTokenSource();
var host_mixer = new SimpleAudioMixer();
var host_format = new WaveFormat();
var host_buffer = new BufferedWaveProvider(host_format){
    BufferDuration = TimeSpan.FromSeconds(1),
    DiscardOnBufferOverflow = true
};
host_mixer.BeginMixingTo(host_buffer, cts);

using var server = new SoundServer(host_format, cts);
server.Name = user_name;

var running = true;
server.Player.Player.OutEvent.PlaybackStopped += (_, _) => running = false;

var is_recording = false;
using var wave_in = new WaveInEvent();
var wave_in_buffer = new BufferedWaveProvider(wave_in.WaveFormat);
wave_in.RecordingStopped += (_, _) =>
{
    is_recording = false;
};
wave_in.DataAvailable += (_, data) =>
{
    wave_in_buffer.AddSamples(data.Buffer, 0, data.BytesRecorded);
};
var wave_in_sample_id = host_mixer.AddSample(new SampleInfo(wave_in_buffer.ToSampleProvider()));
Console.WriteLine($"添加语音输入，ID为：{wave_in_sample_id}");

var remote_end_points = new ConcurrentBag<EndPoint>();
if (!ThreadPool.QueueUserWorkItem(ReceiveTask))
{
    Console.WriteLine("无法运行数据接收服务");
    return;
}

if (!ThreadPool.QueueUserWorkItem(HeartbeatTask))
{
    Console.WriteLine("无法运行心跳包发送服务");
    return;
}

if (!ThreadPool.QueueUserWorkItem(BroadcastTask))
{
    Console.WriteLine("无法运行声音广播服务");
    return;
}
    
while (running)
{
    Console.Write(">> ");
    var line = Console.ReadLine();
    if (line is null || line.Length == 0 || line.StartsWith('#')) continue;

    var first_space_index = line.IndexOf(' ');
    var cmd = (first_space_index < 0 ?  line : line[..first_space_index]).Trim().ToLower();
    var content = (first_space_index < 0 ? "" : line[first_space_index..]).Trim();
    switch (cmd)
    {
        case "add":
            AddRemoteEndpoint(content);
            break;

        case "speak":
            EnableMicrophone(content);
            break;

        case "mute":
            MuteMicrophone(content);
            break;

        case "play":
            PlayFile(content);
            break;

        case "stop":
            StopFile(content);
            break;
    }
}

return;

void AddRemoteEndpoint(string content)
{
    if (IPEndPoint.TryParse(content, out var ep))
    {
        remote_end_points.Add(ep);
    }
    else
    {
        Console.WriteLine($"远程主机({content})添加失败");
    }
}

void EnableMicrophone(string content)
{
    if (is_recording)
    {
        return;
    }

    is_recording = true;
    wave_in.StartRecording();
}

void MuteMicrophone(string content)
{
    if (!is_recording)
    {
        return;
    }
    wave_in.StopRecording();
}

void PlayFile(string content)
{
    if (!File.Exists(content))
    {
        Console.WriteLine($"找不到要播放的文件({content})");
    }

    ThreadPool.QueueUserWorkItem(_ =>
    {
        using var audio = new AudioFileReader(content);
        var id = host_mixer.AddSample(new SampleInfo(audio));
        Console.WriteLine($"成功添加音乐文件输入，ID为：{id}");
        Thread.Sleep(TimeSpan.FromMinutes(10));
        host_mixer.RemoveSample(id);
    });
}

void StopFile(string content)
{

}

void ReceiveTask(object? state)
{
    var buffer = new byte[PAYLOAD_SIZE * 4];
    while (!cts.IsCancellationRequested)
    {
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        var read_bytes = socket.ReceiveFrom(buffer, ref ep);
        var data = new byte[read_bytes];
        Array.Copy(buffer, data, read_bytes);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            server.HandleClientMessage(socket, ep, data);
        });
        Thread.Sleep(TimeSpan.FromMilliseconds(1));
    }
}

void HeartbeatTask(object? state)
{
    while (!cts.IsCancellationRequested)
    {
        foreach (var ep in remote_end_points)
        {
            server.SendHeartbeatMessage(socket, ep);
        }
        Thread.Sleep(TimeSpan.FromSeconds(5));
    }
}

void BroadcastTask(object? state)
{
    var header_size = Marshal.SizeOf(typeof(SoundPackageHeader));
    var buffer = new byte[PAYLOAD_SIZE + header_size];
    var header = new SoundPackageHeader
    {
        SampleRate = host_format.SampleRate,
        Channels = host_format.Channels,
        SampleBits = host_format.BitsPerSample
    };
    while (!cts.IsCancellationRequested)
    {
        header.DataLength = host_buffer.Read(buffer, header_size, PAYLOAD_SIZE);
        header.Timestamp = DateTime.UtcNow;
        Array.Copy(header.ToBytes(), buffer, header_size);
        server.SendMessage(socket, buffer);
        Thread.Sleep(TimeSpan.FromMilliseconds(100));
    }
}
