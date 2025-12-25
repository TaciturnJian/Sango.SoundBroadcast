using System.Collections.Concurrent;
using Concentus;

using NAudio.Wave;

using Sango.SoundBroadcast.Core;

using System.Net;
using System.Net.Sockets;
using Whisper.net;

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

const int SampleRate = 16000;
const int SampleBits = 16;
const int Channels = 1;
const int BufferDurationMs = 20;
const int FrameSize = SampleRate / (1000 / BufferDurationMs);
const int Payload = FrameSize * 2;

var whisper_queue = new ConcurrentQueue<byte[]>();
var format = new WaveFormat(SampleRate, Channels);
var voice_buffer = new byte[Payload];
var clients = new Dictionary<IPEndPoint, float>();
var music_play_cts = new CancellationTokenSource();
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
        var first_space_index = line.IndexOf(' ');
        var ep_str = first_space_index < 0 ? line : line[..first_space_index].Trim();
        var volume_str = first_space_index < 0 ? "1.0" : line[first_space_index..].Trim();

        if (!IPEndPoint.TryParse(ep_str, out var client))
        {
            Console.WriteLine($"在解析客户端地址时发生错误，字符串({line})可能不是正确的地址");
            continue;
        }

        if (!float.TryParse(volume_str, out var volume))
        {
            Console.WriteLine($"在解析客户端音量时发生错误，字符串({volume_str})可能不是正确的浮点数");
            volume = 1.0f;
        }

        Console.WriteLine($"将目标客户端({client.Serialize()})添加到广播列表");
        clients.Add(client, volume);
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
    ReceiveTask();
});

var whisper_model = "./ggml-small.bin";
Console.WriteLine($"正在加载文本转语音模型文件({whisper_model})");
using var whisper_factory = WhisperFactory.FromPath(whisper_model);
using var whisper_processor = whisper_factory.CreateBuilder()
    .WithLanguage("zh")
    .Build();
ThreadPool.QueueUserWorkItem(async _ =>
{
    Console.WriteLine("正在执行语音转文本服务");
    var max = 1024 * 40;
    var buffer = new float[max];
    var buffer_index = 0;
    var threshold = max - 1024 * 2;
    while (true)
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(1));
        if (!whisper_queue.TryDequeue(out var package)) continue;
        for (var i = 0; i + 1 < package.Length; i += 2)
        {
            var short_sample = BitConverter.ToInt16(package, i);
            buffer[buffer_index ++] = short_sample / 32768.0f;
        }

        if (buffer_index < threshold)
        {
            continue;
        }

        await foreach (var segment in whisper_processor.ProcessAsync(new ReadOnlyMemory<float>(buffer, 0, buffer_index)))
        {
            Console.WriteLine($"语音识别结果({segment.Start}->{segment.End})({segment.NoSpeechProbability}): {segment.Text}");
        }

        buffer_index = 0;
    }
});

Thread.Sleep(TimeSpan.FromSeconds(2)); // 可以改成1，加载会快一秒

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

void ReceiveTask()
{
    using var decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);
    while (true)
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(0));
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var received_bytes = udp.Receive(ref remote);
            //Console.WriteLine($"从({remote.Serialize()})接收到{received_bytes.Length}字节的数据");

            var pcm = new short[Payload * Channels];
            var samples = decoder.Decode(received_bytes, pcm, FrameSize);

            var audio_bytes = new byte[samples * sizeof(short)];
            Buffer.BlockCopy(pcm, 0, audio_bytes, 0, audio_bytes.Length);
            if (clients.TryGetValue(remote, out var volume))
            {
                AdjustVolume(audio_bytes, audio_bytes.Length, volume);
            }

            sounder.WaveProvider.AddSamples(audio_bytes, 0, audio_bytes.Length);
        }
        catch (Exception e)
        {
            Console.WriteLine($"在接收数据时发生错误：{e}");
        }
    }
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

static void AdjustVolume(byte[] buffer, int bytes, float volume)
{
    for (var i = 0; i < bytes; i += 2)
    {
        if (i + 1 >= bytes) break;

        var sample = BitConverter.ToInt16(buffer, i);
        var adjusted = sample * volume;
        adjusted = Math.Clamp(adjusted, short.MinValue, short.MaxValue);
        var adjusted_bytes = BitConverter.GetBytes((short)adjusted);
        buffer[i] = adjusted_bytes[0];
        buffer[i + 1] = adjusted_bytes[1];
    }
}

void PlayMusic(string file, float volume)
{
    music_play_cts.Dispose();
    music_play_cts = new CancellationTokenSource();

    using var audio = new AudioFileReader(file);
    using var convert_stream = new WaveFormatConversionStream(
        format,
        new ResamplerDmoStream(audio, format)
    );

    var audio_buffer = new byte[Payload];

    var sleep_time = TimeSpan.FromMilliseconds(1000.0 * Payload / (format.SampleRate * sizeof(short)));
    var end_time = DateTime.UtcNow;
    while (!music_play_cts.IsCancellationRequested)
    {
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
        AdjustVolume(audio_buffer, bytes, volume);
        sounder.WaveProvider.AddSamples(audio_buffer, 0, bytes);
        BroadcastData(audio_buffer, bytes);
    }
}

void SendDataToWhisper(byte[] buffer, int offset, int length)
{
    var bytes = new byte[length];
    Buffer.BlockCopy(buffer, offset, bytes, 0, bytes.Length);

    var threshold = 32;
    if (whisper_queue.Count > threshold)
    {
        whisper_queue.Clear();
    }
    whisper_queue.Enqueue(bytes);
}

void BroadcastData(byte[] buffer, int bytesRecorded)
{
    SendDataToWhisper(buffer, 0, bytesRecorded);
    var samples = bytesRecorded / 2;
    var sample_buffer = new short[samples];
    Buffer.BlockCopy(buffer, 0, sample_buffer, 0, bytesRecorded);
    var encoded_bytes = voice_encoder.Encode(sample_buffer, FrameSize, voice_buffer, Payload);
    foreach (var client in clients)
    {
        try
        {
            //Console.WriteLine($"正在向({client.Key.Serialize()})发送音频包({encoded_bytes}字节)");
            udp.Send(voice_buffer, encoded_bytes, client.Key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"向({client.Key.Serialize()})发送数据包失败：{ex}");
        }
    }
}
