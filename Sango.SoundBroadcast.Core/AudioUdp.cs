using Concentus;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NAudio.CoreAudioApi.Interfaces;

namespace Sango.SoundBroadcast.Core;

public class AudioUdp
{
    public const int SampleRate = 16000;
    public const int SampleBits = 16;
    public const int Channels = 1;
    public const int BufferDurationMs = 20;
    public const int FrameSize = SampleRate / (1000 / BufferDurationMs);
    public const int Payload = FrameSize * 2;

    public static WaveFormat Format { get; } = new(SampleRate, Channels);

    public static void ReceiveTask(UdpClient udp)
    {
        using var sounder = new SimpleSoundPlayer(Format);
        using var decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);
        while (true)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(0));
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var received_bytes = udp.Receive(ref remote);
                Console.WriteLine($"从({remote.Serialize()})接收到{received_bytes.Length}字节的数据");

                var pcm = new short[Payload * Channels];
                var samples = decoder.Decode(received_bytes, pcm, FrameSize);

                var audio_bytes = new byte[samples * sizeof(short)];
                Buffer.BlockCopy(pcm, 0, audio_bytes, 0, audio_bytes.Length);

                sounder.WaveProvider.AddSamples(audio_bytes, 0, audio_bytes.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine($"在接收数据时发生错误：{e}");
            }
        }
    }

    public static void BeginBroadcast(UdpClient udp, string app, IEnumerable<IPEndPoint> clients)
    {
        var devices = new MMDeviceEnumerator();
        var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        AudioSessionControl? target_session = null;
        for (var id = 0; id < sessions.Count; ++id)
        {
            try
            {
                var session = sessions[id];
                if (session.GetProcessID == 0) continue;
                var process = Process.GetProcessById((int)session.GetProcessID);
                if (!process.ProcessName.Contains(app)) continue;
                target_session = session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"访问目标音频会话({id})失败：{ex}");
            }
        }

        BeginAppCapture();

        var voice_buffer = new byte[Payload];
        var voice_encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels);
        var wave_in = new WaveInEvent { BufferMilliseconds = BufferDurationMs, WaveFormat = Format };
        wave_in.DataAvailable += (sender, e) =>
        {
            var samples = e.BytesRecorded / 2;
            var sample_buffer = new short[samples];
            Buffer.BlockCopy(e.Buffer, 0, sample_buffer, 0, e.BytesRecorded);
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
        };
        wave_in.StartRecording();
        return;

        void BeginAppCapture()
        {
            if (target_session is null)
            {
                Console.WriteLine($"找不到目标({app})的音频会话，将禁用程序音频捕获服务");
                return;
            }

            var app_buffer = new byte[Payload];
            var app_encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels);
            var app_capture = new WasapiLoopbackCapture(device) { WaveFormat = Format, };
            app_capture.DataAvailable += (sender, e) =>
            {
                var is_target = target_session.State == AudioSessionState.AudioSessionStateActive &&
                                !target_session.SimpleAudioVolume.Mute;
                if (!is_target)
                {
                    return;
                }

                var samples = e.BytesRecorded / 2;
                var sample_buffer = new short[samples];
                Buffer.BlockCopy(e.Buffer, 0, sample_buffer, 0, e.BytesRecorded);
                var encoded_bytes = app_encoder.Encode(sample_buffer, FrameSize, app_buffer, Payload);
                foreach (var client in clients)
                {
                    try
                    {
                        Console.WriteLine($"正在向({client.Serialize()})发送App音频包({encoded_bytes}字节)");
                        udp.Send(app_buffer, encoded_bytes, client);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"向({client.Serialize()})发送数据包失败：{ex}");
                    }
                }
            };
            app_capture.StartRecording();
        }
    }
}