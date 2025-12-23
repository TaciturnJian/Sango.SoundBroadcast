using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Sango.SoundBroadcast.Core;

public class SimpleAudioMixer
{
    private readonly object Mutex = new();
    private readonly Dictionary<Guid, SampleInfo> Samples = new();

    public class MixerBuffer
    {
        public const int SampleBufferSizeForOneChannel = 1024;
        public const int SampleBufferSize = 2 * SampleBufferSizeForOneChannel;
        private readonly float[] WorkBuffer = new float[SampleBufferSize];
        private readonly float[] ResultBuffer = new float[SampleBufferSize];
        private int MaxRead;

        public void ClearResult()
        {
            Array.Fill(ResultBuffer, 0);
            MaxRead = 0;
        }

        public void MixSample(SampleInfo info, WaveFormat outputFormat)
        {
            var sample_provider = info.Provider;
            if (sample_provider.WaveFormat.SampleRate != outputFormat.SampleRate)
            {
                sample_provider = new WdlResamplingSampleProvider(sample_provider, outputFormat.SampleRate);
            }

            if (sample_provider.WaveFormat.Channels != outputFormat.Channels)
            {
                sample_provider = outputFormat.Channels switch
                {
                    1 => sample_provider.ToMono(),
                    2 => sample_provider.ToStereo(),
                    _ => sample_provider
                };
            }

            var read_count = sample_provider.Read(WorkBuffer, 0, WorkBuffer.Length);
            for (var i = 0; i < read_count; ++i)
                ResultBuffer[i] += WorkBuffer[i] * info.Gain;
            if (read_count > MaxRead) MaxRead = read_count;
        }

        public void WriteResultTo(BufferedWaveProvider wave)
        {
            for (var i = 0; i < MaxRead; ++i)
                WriteSampleToWave(ResultBuffer[i], wave);
        }

        public static TimeSpan GetSleepTime(WaveFormat outputFormat)
        {
            var ms = SampleBufferSizeForOneChannel * 1000 / outputFormat.SampleRate;
            return TimeSpan.FromMilliseconds(ms);
        }

        public static void WriteSampleToWave(float sample, BufferedWaveProvider wave)
        {
            var value = (short)(Math.Clamp(sample, -1.0f, +1.0f) * short.MaxValue);
            var bytes = BitConverter.GetBytes(value);
            wave.AddSamples(bytes, 0, bytes.Length);
        }
    }

    public Guid AddSample(SampleInfo info)
    {
        lock (Mutex)
        {
            var guid = Guid.NewGuid();
            Samples.Add(guid, info);
            return guid;
        }
    }

    public bool RemoveSample(Guid id)
    {
        lock (Mutex)
        {
            return Samples.Remove(id);
        }
    }

    public bool BeginMixingTo(BufferedWaveProvider output, CancellationTokenSource cts)
    {
        return ThreadPool.QueueUserWorkItem(_ =>
        {
            var sleep_time = MixerBuffer.GetSleepTime(output.WaveFormat);
            var current_time = DateTime.UtcNow;
            var end_time = current_time + sleep_time;
            var buffer = new MixerBuffer();
            while (!cts.IsCancellationRequested)
            {
                while (current_time < end_time)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(2));
                    current_time = DateTime.UtcNow;
                }

                end_time += sleep_time;
                MixTo(output, buffer);
            }
        });
    }

    private void MixTo(BufferedWaveProvider output, MixerBuffer buffer)
    {
        lock (Mutex)
        {
            if (Samples.Count == 0) return;
            buffer.ClearResult();
            foreach (var pair in Samples)
                buffer.MixSample(pair.Value, output.WaveFormat);
            buffer.WriteResultTo(output);
        }
    }
}