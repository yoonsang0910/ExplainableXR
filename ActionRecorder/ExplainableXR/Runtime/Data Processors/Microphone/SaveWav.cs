//Reference: WAV file format from http://soundfile.sapp.org/doc/WaveFormat/
//Reference: https://github.com/AgrMayank/AudioRecorder/

using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Whisper.Utils;

public class SaveWav
{
    public static byte[] Save(string filename, AudioClip clip)
    {
        if (!clip)
        {
            Debug.LogError("SaveWav: AudioClip is null! Cannot save.");
            return null;
        }

        if (!filename.ToLower().EndsWith(".wav"))
        {
            filename += ".wav";
        }

        using var memoryStream = CreateEmptyWavFile();
        ConvertAndWrite(memoryStream, clip);
        WriteWavHeader(memoryStream, clip);
        return memoryStream.ToArray();
    }

    private static MemoryStream CreateEmptyWavFile()
    {
        var memoryStream = new MemoryStream();
        for (var i = 0; i < 44; i++)
        {
            memoryStream.WriteByte(0);
        }

        return memoryStream;
    }

    private static void ConvertAndWrite(MemoryStream memoryStream, AudioClip clip)
    {
        if (!clip)
        {
            Debug.LogError("SaveWav: AudioClip is null! Cannot convert.");
            return;
        }

        var samples = new float[clip.samples];
        clip.GetData(samples, 0);

        var intData = new short[samples.Length];
        var bytesData = new byte[samples.Length * 2];

        var rescaleFactor = 32767;
        for (var i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            BitConverter.GetBytes(intData[i]).CopyTo(bytesData, i * 2);
        }

        memoryStream.Write(bytesData, 0, bytesData.Length);
    }

    private static void WriteWavHeader(MemoryStream memoryStream, AudioClip clip)
    {
        memoryStream.Seek(0, SeekOrigin.Begin);
        memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(memoryStream.Length - 8), 0, 4);
        memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
        memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(16), 0, 4);
        memoryStream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
        memoryStream.Write(BitConverter.GetBytes(clip.channels), 0, 2);
        memoryStream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(clip.frequency * clip.channels * 2), 0, 4);
        memoryStream.Write(BitConverter.GetBytes((ushort)(clip.channels * 2)), 0, 2);
        memoryStream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
        memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(clip.samples * clip.channels * 2), 0, 4);
    }
}

public static class AudioClipWavConverter
{
    // Asynchronously converts an AudioChunk to a WAV file byte array.
    // The output WAV is 16-bit PCM with the same sample rate and channel count as the AudioChunk.
    public async static Task<byte[]> ConvertAudioChunkToWavAsync(AudioChunk chunk, string fullSavePath = null)
    {
        // On the main thread, retrieve the necessary values.
        int sampleCount = chunk.Data.Length; // Only use the actual recorded samples
        int channels = chunk.Channels;
        int sampleRate = chunk.Frequency;

        // Offload the heavy conversion work to a background thread.
        byte[] wavBytes = await Task.Run(() =>
        {
            // Convert the float samples (range [-1, 1]) to 16-bit PCM
            short[] intData = new short[sampleCount];
            byte[] bytesData = new byte[sampleCount * 2];
            float rescaleFactor = 32767f;
            for (int i = 0; i < sampleCount; i++)
            {
                intData[i] = (short)(chunk.Data[i] * rescaleFactor);
            }
            Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);

            // Build the WAV header (44 bytes)
            int headerSize = 44;
            byte[] header = new byte[headerSize];

            // "RIFF" chunk descriptor
            header[0] = (byte)'R';
            header[1] = (byte)'I';
            header[2] = (byte)'F';
            header[3] = (byte)'F';

            // Chunk size: file size minus 8 bytes for "RIFF" and size
            int fileSize = headerSize + bytesData.Length;
            int chunkSize = fileSize - 8;
            header[4] = (byte)(chunkSize & 0xff);
            header[5] = (byte)((chunkSize >> 8) & 0xff);
            header[6] = (byte)((chunkSize >> 16) & 0xff);
            header[7] = (byte)((chunkSize >> 24) & 0xff);

            // "WAVE" format
            header[8] = (byte)'W';
            header[9] = (byte)'A';
            header[10] = (byte)'V';
            header[11] = (byte)'E';

            // "fmt " subchunk
            header[12] = (byte)'f';
            header[13] = (byte)'m';
            header[14] = (byte)'t';
            header[15] = (byte)' ';

            // Subchunk1Size (16 for PCM)
            header[16] = 16;
            header[17] = 0;
            header[18] = 0;
            header[19] = 0;

            // AudioFormat (1 for PCM)
            header[20] = 1;
            header[21] = 0;

            // Number of channels
            header[22] = (byte)(channels & 0xff);
            header[23] = (byte)((channels >> 8) & 0xff);

            // Sample rate
            header[24] = (byte)(sampleRate & 0xff);
            header[25] = (byte)((sampleRate >> 8) & 0xff);
            header[26] = (byte)((sampleRate >> 16) & 0xff);
            header[27] = (byte)((sampleRate >> 24) & 0xff);

            // Byte rate = SampleRate * NumChannels * BitsPerSample/8
            int byteRate = sampleRate * channels * 16 / 8;
            header[28] = (byte)(byteRate & 0xff);
            header[29] = (byte)((byteRate >> 8) & 0xff);
            header[30] = (byte)((byteRate >> 16) & 0xff);
            header[31] = (byte)((byteRate >> 24) & 0xff);

            // Block align = NumChannels * BitsPerSample/8
            ushort blockAlign = (ushort)(channels * 16 / 8);
            header[32] = (byte)(blockAlign & 0xff);
            header[33] = (byte)((blockAlign >> 8) & 0xff);

            // Bits per sample (16 bits)
            ushort bitsPerSample = 16;
            header[34] = (byte)(bitsPerSample & 0xff);
            header[35] = (byte)((bitsPerSample >> 8) & 0xff);

            // "data" subchunk
            header[36] = (byte)'d';
            header[37] = (byte)'a';
            header[38] = (byte)'t';
            header[39] = (byte)'a';

            // Subchunk2Size: number of samples * (BitsPerSample/8)
            int subChunk2Size = bytesData.Length;
            header[40] = (byte)(subChunk2Size & 0xff);
            header[41] = (byte)((subChunk2Size >> 8) & 0xff);
            header[42] = (byte)((subChunk2Size >> 16) & 0xff);
            header[43] = (byte)((subChunk2Size >> 24) & 0xff);

            // Combine header and data into one byte array
            byte[] wavBytes = new byte[headerSize + bytesData.Length];
            Buffer.BlockCopy(header, 0, wavBytes, 0, headerSize);
            Buffer.BlockCopy(bytesData, 0, wavBytes, headerSize, bytesData.Length);

            return wavBytes;
        });

        if (!string.IsNullOrEmpty(fullSavePath))
            await File.WriteAllBytesAsync(fullSavePath, wavBytes);

        return wavBytes;

    }
}
