using Concentus;

using NAudio.Wave;

using Sango.SoundBroadcast.Core;

using System.Net;
using System.Net.Sockets;

if (args.Length < 2)
{
    Console.WriteLine("使用方式：\n\t<程序名> <监听地址> <捕获程序名称|!none> [广播客户端列表文件]");
    return;
}

var host_address = args[0];
if (!IPEndPoint.TryParse(host_address, out var host_endpoint))
{
    Console.WriteLine($"在解析主机地址({host_address})时出错");
    return;
}

var app_name = args[1];

const int SampleRate = 16000;
const int SampleBits = 16;
const int Channels = 1;
const int BufferDurationMs = 20;
const int FrameSize = SampleRate / (1000 / BufferDurationMs);
const int Payload = FrameSize * 2;

var format = new WaveFormat(SampleRate, Channels);
var voice_buffer = new byte[Payload];
var clients = new List<IPEndPoint>();
using var music_play_cts = new CancellationTokenSource();
using var voice_encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels);
using var udp = new UdpClient(host_endpoint);
using var sounder = new SimpleSoundPlayer(AudioUdp.Format);
using var wave_in = new WaveInEvent { BufferMilliseconds = BufferDurationMs, WaveFormat = format };
wave_in.DataAvailable += (_, e) =>
{
    BroadcastData(e.Buffer, e.BytesRecorded);
};

var mute = args.Length < 3;
if (!mute)
{
    var broadcast_client_file = args[2];
    if (!File.Exists(broadcast_client_file))
    {
        Console.WriteLine($"找不到广播客户端列表文件({broadcast_client_file})");
        return;
    }

    foreach (var line in File.ReadAllLines(broadcast_client_file))
    {
        if (line.StartsWith('#') || line.Trim().Length == 0) continue;
        if (!IPEndPoint.TryParse(line, out var client))
        {
            Console.WriteLine($"在解析客户端地址时发生错误，字符串({line})可能不是正确的地址");
            continue;
        }

        Console.WriteLine($"将目标客户端({client.Serialize()})添加到广播列表");
        clients.Add(client);
    }

    if (clients.Count == 0)
    {
        Console.WriteLine("找不到要广播的客户端");
        return;
    }

    Console.WriteLine("正在执行广播任务");
}

ThreadPool.QueueUserWorkItem(_ =>
{
    Console.WriteLine("正在执行接收任务");
    AudioUdp.ReceiveTask(udp, sounder);
});

while (true)
{
    Console.Write(">> ");
    var line = Console.ReadLine();
    if (line is null) continue;
    line = line.Trim();
    if (line.Length == 0) continue;
    var first_space_index = line.IndexOf(' ');
    var cmd = first_space_index < 0 ? line : line[..first_space_index].Trim();
    var rest = first_space_index < 0 ? "" : line[first_space_index..].Trim();
    ProcessCommand(cmd.ToLower(), rest);
}


void ProcessCommand(string cmd, string rest)
{
    if (cmd == "mute")
    {
        wave_in.StopRecording();
        return;
    }

    if (cmd == "speak")
    {
        wave_in.StartRecording();
        return;
    }

    if (cmd == "play")
    {
        var first_space_index = rest.IndexOf(' ');
        var file = first_space_index < 0 ? rest : rest[..first_space_index].Trim();
        var volume_str = first_space_index < 0 ? "1.0" : rest[first_space_index..].Trim();
        var volume = float.TryParse(volume_str, out var result) ? result : 1.0f;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            PlayMusic(file, volume);
        });
        return;
    }

    if (cmd == "stop")
    {
        music_play_cts.Cancel();
        return;
    }

    Console.WriteLine($"未知的命令：{cmd}");
}

void PlayMusic(string file, float volume)
{
    using var audio = new AudioFileReader(file);
    using var convert_stream = new WaveFormatConversionStream(
        format,
        new ResamplerDmoStream(audio, format)
    );
    music_play_cts.TryReset();

    var audio_buffer = new byte[Payload];

    var sleep_time = TimeSpan.FromMilliseconds(1000.0 * Payload / (format.SampleRate * sizeof(short)));
    var end_time = DateTime.UtcNow;
    while (true)
    {
        if (music_play_cts.IsCancellationRequested)
        {
            return;
        }
        var current_time = DateTime.UtcNow;
        while (current_time < end_time)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            current_time = DateTime.UtcNow;
        }

        end_time += sleep_time;
        var bytes = convert_stream.Read(audio_buffer, 0, Payload);
        if (bytes == 0) 
            break;
        /*for (var i = 0; i < bytes; i += 1)
        {
            audio_buffer[i] = (byte)(audio_buffer[i] * volume);
        }*/
        sounder.WaveProvider.AddSamples(audio_buffer, 0, bytes);
        BroadcastData(audio_buffer, bytes);
    }
}

void BroadcastData(byte[] buffer, int bytesRecorded)
{
    var samples = bytesRecorded / 2;
    var sample_buffer = new short[samples];
    Buffer.BlockCopy(buffer, 0, sample_buffer, 0, bytesRecorded);
    var encoded_bytes = voice_encoder.Encode(sample_buffer, FrameSize, voice_buffer, Payload);
    foreach (var client in clients)
    {
        try
        {
            Console.WriteLine($"正在向({client.Serialize()})发送音频包({encoded_bytes}字节)");
            udp.Send(voice_buffer, encoded_bytes, client);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"向({client.Serialize()})发送数据包失败：{ex}");
        }
    }
}
