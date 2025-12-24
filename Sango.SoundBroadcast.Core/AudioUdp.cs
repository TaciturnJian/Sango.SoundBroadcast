using Concentus;

using NAudio.CoreAudioApi;
using NAudio.Mixer;
using NAudio.Wave;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NAudio.CoreAudioApi.Interfaces;

namespace Sango.SoundBroadcast.Core;

using NAudio.Wave;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public class AudioSegment
{
    public WaveFormat WaveFormat { get; set; } = new();
    public byte[] AudioData { get; set; } = [];
    public int Volume { get; set; }
}

public class Sounder
{
    public string TargetAppName { get; private set; } = "do_not_capture_app";

    public WaveFormat TargetFormat { get; } = new(16000, 1);

    public AudioSessionControl? TargetSession { get; private set; }

    public BufferedWaveProvider MixerBuffer { get; private set; }

    public WasapiLoopbackCapture AppCapture { get; private set; }

    public WaveInEvent MicrophoneCapture { get; private set; }

    public Sounder()
    {
        MixerBuffer = new BufferedWaveProvider(TargetFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(3),
            DiscardOnBufferOverflow = true,
        };

        AppCapture = new WasapiLoopbackCapture();
        AppCapture.WaveFormat = TargetFormat;
        AppCapture.DataAvailable += OnAppDataAvailable;
        AppCapture.StartRecording();

        MicrophoneCapture = new WaveInEvent();
        MicrophoneCapture.WaveFormat = TargetFormat;
        MicrophoneCapture.DataAvailable += OnMicrophoneCaptureDataAvailable;
        MicrophoneCapture.StartRecording();
    }

    public static AudioSessionControl? FindTargetSession(string name)
    {
        return null;
    }

    private void OnAppDataAvailable(object? sender, WaveInEventArgs e)
    {
    }
    
    private void OnMicrophoneCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
    }
    
    public BufferedWaveProvider GetMixedAudio()
    {
        return MixerBuffer;
    }
    
    private byte[] ConvertAudioFormat(byte[] input, WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        return input;
    }

    public void Dispose()
    {
        AppCapture.Dispose();
        MicrophoneCapture.Dispose();
    }
}

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
                                !target_session.SimpleAudioVolume.Mute &&
                                target_session.SimpleAudioVolume.Volume > 0;
                if (!is_target)
                {
                    return;
                }

                if (target_session.State == AudioSessionState.AudioSessionStateActive) {}
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
        }
    }

    public static short[] SynthesizeAudio(List<AudioSegment> segments, int outputSampleRate)
    {
        if (segments.Count == 0)
            return [];

        var output_format = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate, 1);
        var converted_segments = new List<float[]>();

        foreach (var segment in segments)
        {
            var converted = ConvertSegment(segment, outputSampleRate);
            ApplyVolume(converted, segment.Volume / 100f);
            converted_segments.Add(converted);
        }

        var max_length = converted_segments.Max(s => s.Length);
        var mixed = new float[max_length];

        foreach (var segment in converted_segments)
        {
            for (var i = 0; i < segment.Length; i++)
            {
                mixed[i] += segment[i];
            }
        }

        return ConvertTo16Bit(mixed);
    }

    private static float[] ConvertSegment(AudioSegment segment, int targetSampleRate)
    {
        using var raw_stream = new RawSourceWaveStream(
            segment.AudioData, 0, segment.AudioData.Length, segment.WaveFormat);

        if (segment.WaveFormat.SampleRate == targetSampleRate &&
            segment.WaveFormat.Channels == 1)
        {
            return ConvertToFloat(segment.AudioData, segment.WaveFormat);
        }

        using var resampler = new MediaFoundationResampler(raw_stream, targetSampleRate);
        resampler.ResamplerQuality = 60;

        var mono_stream = new StereoToMonoProvider16(resampler);

        using var mem_stream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(mem_stream, mono_stream);

        var resampled_bytes = mem_stream.ToArray();
        const int DATA_START = 44;
        var audio_data = new byte[resampled_bytes.Length - DATA_START];
        Array.Copy(resampled_bytes, DATA_START, audio_data, 0, audio_data.Length);

        return ConvertToFloat(audio_data, mono_stream.WaveFormat);
    }

    private static float[] ConvertToFloat(byte[] audioData, WaveFormat format)
    {
        var bytes_per_sample = format.BitsPerSample / 8;
        var sample_count = audioData.Length / bytes_per_sample;
        var samples = new float[sample_count];

        for (var i = 0; i < sample_count; i++)
        {
            var byte_index = i * bytes_per_sample;

            switch (format.Encoding)
            {
                case WaveFormatEncoding.IeeeFloat:
                    samples[i] = BitConverter.ToSingle(audioData, byte_index);
                    break;
                case WaveFormatEncoding.Pcm when format.BitsPerSample == 16:
                {
                    var sample = BitConverter.ToInt16(audioData, byte_index);
                    samples[i] = sample / 32768f;
                    break;
                }
                case WaveFormatEncoding.Pcm when format.BitsPerSample == 8:
                    samples[i] = (audioData[byte_index] - 128) / 128f;
                    break;
                case WaveFormatEncoding.Pcm when format.BitsPerSample == 24:
                {
                    var sample = (audioData[byte_index] | (audioData[byte_index + 1] << 8) |
                                  (audioData[byte_index + 2] << 16));
                    if (sample > 0x7FFFFF) sample -= 0x1000000;
                    samples[i] = sample / 8388608f;
                    break;
                }
                case WaveFormatEncoding.Pcm:
                {
                    if (format.BitsPerSample == 32)
                    {
                        var sample = BitConverter.ToInt32(audioData, byte_index);
                        samples[i] = sample / 2147483648f;
                    }

                    break;
                }
                default:
                    Console.WriteLine($"ConvertToFloat：不支持的音频格式：{format}");
                    break;
            }
        }

        if (format.Channels <= 1) return samples;

        var mono = new float[sample_count / format.Channels];
        for (var i = 0; i < mono.Length; i++)
        {
            float sum = 0;
            for (var ch = 0; ch < format.Channels; ch++)
            {
                sum += samples[i * format.Channels + ch];
            }

            mono[i] = sum / format.Channels;
        }

        return mono;
    }

    private static void ApplyVolume(float[] samples, float volume)
    {
        volume = Math.Max(0, Math.Min(3, volume));
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= volume;
            samples[i] = Math.Max(-1, Math.Min(1, samples[i]));
        }
    }

    private static short[] ConvertTo16Bit(float[] samples)
    {
        var result = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Max(-1, Math.Min(1, samples[i]));
            result[i] = (short)(clamped * short.MaxValue);
        }

        return result;
    }
}