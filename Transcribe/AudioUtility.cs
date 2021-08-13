using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace Transcribe
{
    class AudioUtility
    {
        private AudioUtility()
        { }

        public static byte[] ToPCM16(byte[] inputBuffer, int length, WaveFormat format)
        {
            if (length == 0)
                return new byte[0]; // No bytes recorded, return empty array.

            // Create a WaveStream from the input buffer.
            using var memStream = new MemoryStream(inputBuffer, 0, length);
            using var inputStream = new RawSourceWaveStream(memStream, format);

            // Convert the input stream to a WaveProvider in 16bit PCM format with sample rate of 16000 Hz.
            //var convertedPCM = new WaveFormatConversionStream(new WaveFormat(16000, 16, 1), inputStream);
            var convertedPCM = new StereoToMonoProvider16(
                new SampleToWaveProvider16(
                    new WdlResamplingSampleProvider(
                        new WaveToSampleProvider(inputStream),
                        16000)
                    )
                );

            byte[] convertedBuffer = new byte[length];

            using var stream = new MemoryStream();
            int read;

            // Read the converted WaveProvider into a buffer and turn it into a Stream.
            while ((read = convertedPCM.Read(convertedBuffer, 0, length)) > 0)
                stream.Write(convertedBuffer, 0, read);

            // Return the converted Stream as a byte array.
            return stream.ToArray();
        }
    }
}
