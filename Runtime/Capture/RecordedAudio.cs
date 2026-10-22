using System;

namespace PlaytestNotes
{
    /// <summary>Interleaved samples in [-1, 1] with their format.</summary>
    internal sealed class RecordedAudio
    {
        public RecordedAudio(float[] samples, int frequency, int channels)
        {
            if (frequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency));
            }

            if (channels <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(channels));
            }

            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            Frequency = frequency;
            Channels = channels;
        }

        public float[] Samples { get; }

        public int Frequency { get; }

        public int Channels { get; }

        public float Seconds => Samples.Length / (float)(Frequency * Channels);
    }
}
