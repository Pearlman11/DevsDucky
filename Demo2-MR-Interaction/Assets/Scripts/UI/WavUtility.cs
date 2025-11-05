using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UI
{


    /// <summary>
    /// A utility class to encode a Unity AudioClip into a WAV file byte array.
    /// This is a common implementation found across Unity forums and gists.
    /// </summary>
    public static class WavUtility
    {
        private const int HEADER_SIZE = 44;
        private const ushort BITS_PER_SAMPLE = 16;

        /// <summary>
        /// Encodes an AudioClip into a WAV file format in memory.
        /// </summary>
        /// <param name="clip">The AudioClip to encode.</param>
        /// <returns>A byte array containing the WAV file data.</returns>
        public static byte[] FromAudioClip(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogError("WavUtility: AudioClip is null.");
                return null;
            }

            // 1. Create a MemoryStream to write the data
            using (var stream = new MemoryStream())
            {
                // --- WAV HEADER ---
                // We write a dummy header first, and then seek back to update it
                // with the correct sizes once we have the audio data.
                stream.Write(new byte[HEADER_SIZE], 0, HEADER_SIZE);

                // --- AUDIO DATA ---
                // 2. Get the raw float data from the clip
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                // 3. Convert float samples (-1.0 to 1.0) to 16-bit PCM (int16)
                // This is the format most ASR services expect.
                short[] int16_samples = new short[samples.Length];
                byte[] byte_buffer = new byte[samples.Length * 2]; // 2 bytes per int16

                for (int i = 0; i < samples.Length; i++)
                {
                    // Clamp the float sample, scale it to 16-bit range, and cast to short
                    int16_samples[i] = (short)(Mathf.Clamp(samples[i], -1.0f, 1.0f) * 32767.0f);
                }

                // 4. Write the 16-bit samples into the byte buffer
                Buffer.BlockCopy(int16_samples, 0, byte_buffer, 0, byte_buffer.Length);

                // 5. Write the audio data buffer to the stream
                stream.Write(byte_buffer, 0, byte_buffer.Length);

                // --- UPDATE HEADER ---
                // 6. Now that we have the data size, go back and write the real header
                stream.Seek(0, SeekOrigin.Begin);

                uint dataSize = (uint)byte_buffer.Length;
                uint fileSize = dataSize + HEADER_SIZE - 8;

                // "RIFF" chunk
                stream.Write(Encoding.UTF8.GetBytes("RIFF"), 0, 4);
                stream.Write(BitConverter.GetBytes(fileSize), 0, 4);
                stream.Write(Encoding.UTF8.GetBytes("WAVE"), 0, 4);

                // "fmt " sub-chunk
                stream.Write(Encoding.UTF8.GetBytes("fmt "), 0, 4);
                stream.Write(BitConverter.GetBytes(16), 0, 4); // Sub-chunk 1 size (16 for PCM)
                stream.Write(BitConverter.GetBytes((ushort)1), 0, 2); // Audio format (1 = PCM)
                stream.Write(BitConverter.GetBytes((ushort)clip.channels), 0, 2);
                stream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);

                uint byteRate = (uint)(clip.frequency * clip.channels * (BITS_PER_SAMPLE / 8));
                stream.Write(BitConverter.GetBytes(byteRate), 0, 4);

                ushort blockAlign = (ushort)(clip.channels * (BITS_PER_SAMPLE / 8));
                stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);
                stream.Write(BitConverter.GetBytes(BITS_PER_SAMPLE), 0, 2);

                // "data" sub-chunk
                stream.Write(Encoding.UTF8.GetBytes("data"), 0, 4);
                stream.Write(BitConverter.GetBytes(dataSize), 0, 4); // Sub-chunk 2 size (data size)

                // 7. Return the complete byte array
                return stream.ToArray();
            }
        }
    }
}