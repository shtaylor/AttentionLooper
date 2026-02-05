using System.IO;
using NAudio.Wave;

namespace AttentionLooper.Services;

public static class AudioService
{
    public static float[] ReadPeaks(string filePath, int targetSampleCount = 300)
    {
        if (!File.Exists(filePath))
            return [];

        using var reader = new AudioFileReader(filePath);
        var sampleProvider = reader.ToSampleProvider();
        var channels = reader.WaveFormat.Channels;

        // Read all samples
        var allSamples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i += channels)
            {
                float peak = 0;
                for (int ch = 0; ch < channels && (i + ch) < read; ch++)
                    peak = Math.Max(peak, Math.Abs(buffer[i + ch]));
                allSamples.Add(peak);
            }
        }

        if (allSamples.Count == 0)
            return [];

        // Downsample to targetSampleCount bins
        var peaks = new float[targetSampleCount];
        double samplesPerBin = (double)allSamples.Count / targetSampleCount;

        for (int i = 0; i < targetSampleCount; i++)
        {
            int start = (int)(i * samplesPerBin);
            int end = (int)((i + 1) * samplesPerBin);
            end = Math.Min(end, allSamples.Count);

            float max = 0;
            for (int j = start; j < end; j++)
                max = Math.Max(max, allSamples[j]);

            peaks[i] = max;
        }

        // Normalize to 0-1
        float globalMax = peaks.Max();
        if (globalMax > 0)
        {
            for (int i = 0; i < peaks.Length; i++)
                peaks[i] /= globalMax;
        }

        return peaks;
    }
}
