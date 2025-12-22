namespace Sango.SoundBroadcast.Core;

/// <summary>
/// 音频处理工具类
/// </summary>
public static class AudioUtils
{
    /// <summary>
    /// 将字节数组转换为16位PCM采样数组
    /// </summary>
    public static short[] BytesToSamples16(byte[] bytes, int offset, int length)
    {
        if (length % 2 != 0)
            throw new ArgumentException("Length must be even for 16-bit samples");
            
        int sampleCount = length / 2;
        short[] samples = new short[sampleCount];
            
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(bytes, offset + i * 2);
        }
            
        return samples;
    }
        
    /// <summary>
    /// 将16位PCM采样数组转换为字节数组
    /// </summary>
    public static byte[] SamplesToBytes16(short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
            
        for (int i = 0; i < samples.Length; i++)
        {
            byte[] sampleBytes = BitConverter.GetBytes(samples[i]);
            bytes[i * 2] = sampleBytes[0];
            bytes[i * 2 + 1] = sampleBytes[1];
        }
            
        return bytes;
    }
        
    /// <summary>
    /// 将字节数组转换为32位浮点采样数组
    /// </summary>
    public static float[] BytesToSamplesFloat(byte[] bytes, int offset, int length, int bitsPerSample)
    {
        if (bitsPerSample == 16)
        {
            short[] samples16 = BytesToSamples16(bytes, offset, length);
            float[] samplesFloat = new float[samples16.Length];
                
            for (int i = 0; i < samples16.Length; i++)
            {
                samplesFloat[i] = samples16[i] / 32768f; // 转换为 -1.0 到 1.0
            }
                
            return samplesFloat;
        }
        else if (bitsPerSample == 32)
        {
            if (length % 4 != 0)
                throw new ArgumentException("Length must be multiple of 4 for 32-bit samples");
                
            int sampleCount = length / 4;
            float[] samples = new float[sampleCount];
                
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = BitConverter.ToSingle(bytes, offset + i * 4);
            }
                
            return samples;
        }
        else
        {
            throw new NotSupportedException($"Bits per sample {bitsPerSample} not supported");
        }
    }
        
    /// <summary>
    /// 将浮点采样数组转换为字节数组
    /// </summary>
    public static byte[] SamplesFloatToBytes(float[] samples, int bitsPerSample)
    {
        if (bitsPerSample == 16)
        {
            short[] samples16 = new short[samples.Length];
                
            for (int i = 0; i < samples.Length; i++)
            {
                // 限制在 -1.0 到 1.0 范围内
                float sample = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
                samples16[i] = (short)(sample * 32767);
            }
                
            return SamplesToBytes16(samples16);
        }
        else if (bitsPerSample == 32)
        {
            byte[] bytes = new byte[samples.Length * 4];
                
            for (int i = 0; i < samples.Length; i++)
            {
                // 限制在 -1.0 到 1.0 范围内
                float sample = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
                byte[] sampleBytes = BitConverter.GetBytes(sample);
                    
                bytes[i * 4] = sampleBytes[0];
                bytes[i * 4 + 1] = sampleBytes[1];
                bytes[i * 4 + 2] = sampleBytes[2];
                bytes[i * 4 + 3] = sampleBytes[3];
            }
                
            return bytes;
        }
        else
        {
            throw new NotSupportedException($"Bits per sample {bitsPerSample} not supported");
        }
    }
        
    /// <summary>
    /// 重采样音频数据（简单线性插值）
    /// </summary>
    public static byte[] Resample(byte[] audioData, int originalSampleRate, int targetSampleRate, 
        int channels, int bitsPerSample)
    {
        if (originalSampleRate == targetSampleRate)
            return audioData;
            
        float ratio = (float)targetSampleRate / originalSampleRate;
            
        // 转换为浮点数处理
        float[] samples = BytesToSamplesFloat(audioData, 0, audioData.Length, bitsPerSample);
        int originalSampleCount = samples.Length / channels;
        int targetSampleCount = (int)(originalSampleCount * ratio);
            
        float[] resampled = new float[targetSampleCount * channels];
            
        for (int channel = 0; channel < channels; channel++)
        {
            for (int i = 0; i < targetSampleCount; i++)
            {
                float originalPos = i / ratio;
                int index1 = (int)Math.Floor(originalPos);
                int index2 = Math.Min(index1 + 1, originalSampleCount - 1);
                    
                float weight2 = originalPos - index1;
                float weight1 = 1 - weight2;
                    
                float sample1 = samples[index1 * channels + channel];
                float sample2 = samples[index2 * channels + channel];
                    
                resampled[i * channels + channel] = sample1 * weight1 + sample2 * weight2;
            }
        }
            
        return SamplesFloatToBytes(resampled, bitsPerSample);
    }
        
    /// <summary>
    /// 调整声道数
    /// </summary>
    public static byte[] AdjustChannels(byte[] audioData, int originalChannels, int targetChannels, 
        int bitsPerSample)
    {
        if (originalChannels == targetChannels)
            return audioData;
            
        // 转换为浮点数处理
        float[] samples = BytesToSamplesFloat(audioData, 0, audioData.Length, bitsPerSample);
        int originalSampleCount = samples.Length;
        int samplesPerChannel = originalSampleCount / originalChannels;
            
        float[] adjusted = new float[samplesPerChannel * targetChannels];
            
        if (originalChannels == 1 && targetChannels == 2)
        {
            // 单声道转立体声：复制到两个声道
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float sample = samples[i];
                adjusted[i * 2] = sample;     // 左声道
                adjusted[i * 2 + 1] = sample; // 右声道
            }
        }
        else if (originalChannels == 2 && targetChannels == 1)
        {
            // 立体声转单声道：取平均值
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float left = samples[i * 2];
                float right = samples[i * 2 + 1];
                adjusted[i] = (left + right) * 0.5f;
            }
        }
        else
        {
            // 其他转换：简单复制或丢弃声道
            int minChannels = Math.Min(originalChannels, targetChannels);
            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int channel = 0; channel < minChannels; channel++)
                {
                    adjusted[i * targetChannels + channel] = samples[i * originalChannels + channel];
                }
            }
        }
            
        return SamplesFloatToBytes(adjusted, bitsPerSample);
    }
        
    /// <summary>
    /// 混合多个音频缓冲区
    /// </summary>
    public static byte[] MixBuffers(List<byte[]> buffers, List<float> gains, int bitsPerSample, float masterGain = 1.0f)
    {
        if (buffers == null || buffers.Count == 0)
            return Array.Empty<byte>();
            
        // 找到最长的缓冲区
        int maxLength = 0;
        for (int i = 0; i < buffers.Count; i++)
        {
            if (buffers[i] != null && buffers[i].Length > maxLength)
                maxLength = buffers[i].Length;
        }
            
        if (maxLength == 0)
            return Array.Empty<byte>();
            
        // 将所有缓冲区转换为浮点数
        List<float[]> samplesList = new List<float[]>();
            
        for (int i = 0; i < buffers.Count; i++)
        {
            if (buffers[i] != null && buffers[i].Length > 0)
            {
                float[] samples = BytesToSamplesFloat(buffers[i], 0, buffers[i].Length, bitsPerSample);
                samplesList.Add(samples);
            }
            else
            {
                samplesList.Add(null);
            }
        }
            
        // 混合采样
        int sampleCount = maxLength / (bitsPerSample / 8);
        float[] mixedSamples = new float[sampleCount];
            
        for (int i = 0; i < samplesList.Count; i++)
        {
            if (samplesList[i] != null && gains[i] > 0)
            {
                float gain = gains[i] * masterGain;
                int length = Math.Min(samplesList[i].Length, sampleCount);
                    
                for (int j = 0; j < length; j++)
                {
                    mixedSamples[j] += samplesList[i][j] * gain;
                }
            }
        }
            
        // 限制输出范围，防止削波
        for (int i = 0; i < mixedSamples.Length; i++)
        {
            mixedSamples[i] = Math.Max(-1.0f, Math.Min(1.0f, mixedSamples[i]));
        }
            
        return SamplesFloatToBytes(mixedSamples, bitsPerSample);
    }
}