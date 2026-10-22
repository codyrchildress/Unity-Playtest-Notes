using System;
using System.IO;
using System.Text;

namespace PlaytestNotes
{
    /// <summary>
    /// Writes 16-bit PCM WAV and reads PCM (8, 16, 24, 32-bit) and 32-bit float WAV.
    /// </summary>
    internal static class WavFile
    {
        private const ushort PcmFormat = 1;
        private const ushort FloatFormat = 3;
        private const ushort ExtensibleFormat = 0xFFFE;

        public static byte[] Encode(RecordedAudio audio)
        {
            if (audio == null)
            {
                throw new ArgumentNullException(nameof(audio));
            }

            int dataBytes = audio.Samples.Length * 2;
            using (var stream = new MemoryStream(44 + dataBytes))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write(PcmFormat);
                writer.Write((ushort)audio.Channels);
                writer.Write(audio.Frequency);
                writer.Write(audio.Frequency * audio.Channels * 2);
                writer.Write((ushort)(audio.Channels * 2));
                writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);
                foreach (float sample in audio.Samples)
                {
                    float clamped = sample < -1f ? -1f : (sample > 1f ? 1f : sample);
                    writer.Write((short)Math.Round(clamped * short.MaxValue));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static RecordedAudio Decode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                if (ReadTag(reader) != "RIFF")
                {
                    throw new InvalidDataException("Not a RIFF file.");
                }

                reader.ReadInt32();
                if (ReadTag(reader) != "WAVE")
                {
                    throw new InvalidDataException("Not a WAVE file.");
                }

                ushort format = 0;
                int channels = 0;
                int frequency = 0;
                int bitsPerSample = 0;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string tag = ReadTag(reader);
                    int size = reader.ReadInt32();
                    long next = reader.BaseStream.Position + size + (size & 1);
                    if (tag == "fmt ")
                    {
                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        frequency = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();
                        if (format == ExtensibleFormat && size >= 26)
                        {
                            reader.ReadUInt16();
                            reader.ReadUInt16();
                            reader.ReadUInt32();
                            format = reader.ReadUInt16();
                        }
                    }
                    else if (tag == "data")
                    {
                        if (channels == 0)
                        {
                            throw new InvalidDataException("WAV data chunk comes before its fmt chunk.");
                        }

                        int available = (int)Math.Min(size, reader.BaseStream.Length - reader.BaseStream.Position);
                        return new RecordedAudio(ReadSamples(reader, available, format, bitsPerSample), frequency, channels);
                    }

                    reader.BaseStream.Position = next;
                }

                throw new InvalidDataException("WAV file has no data chunk.");
            }
        }

        private static float[] ReadSamples(BinaryReader reader, int byteCount, ushort format, int bitsPerSample)
        {
            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample == 0)
            {
                throw new InvalidDataException("WAV has 0 bits per sample.");
            }

            var samples = new float[byteCount / bytesPerSample];
            for (int i = 0; i < samples.Length; i++)
            {
                if (format == FloatFormat && bitsPerSample == 32)
                {
                    samples[i] = reader.ReadSingle();
                    continue;
                }

                if (format != PcmFormat)
                {
                    throw new NotSupportedException("WAV format " + format + " is not supported.");
                }

                switch (bitsPerSample)
                {
                    case 8:
                        samples[i] = (reader.ReadByte() - 128) / 128f;
                        break;
                    case 16:
                        samples[i] = reader.ReadInt16() / 32768f;
                        break;
                    case 24:
                        int low = reader.ReadByte() | (reader.ReadByte() << 8);
                        int value = low | ((sbyte)reader.ReadByte() << 16);
                        samples[i] = value / 8388608f;
                        break;
                    case 32:
                        samples[i] = reader.ReadInt32() / 2147483648f;
                        break;
                    default:
                        throw new NotSupportedException(bitsPerSample + "-bit PCM WAV is not supported.");
                }
            }

            return samples;
        }

        private static string ReadTag(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
        }
    }
}
