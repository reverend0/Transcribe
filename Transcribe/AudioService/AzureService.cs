using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Transcribe
{
    class AzureService
    {
        private SpeechConfig speechConfig;
        private DisplayHelper display;

        private SpeechRecognizer micRecognizer;
        private SpeechRecognizer audioRecognizer;
        private PushAudioInputStream audioInputStream;
        private WasapiLoopbackCapture loopbackCapture;

        public AzureService(string serviceKey, string endpoint, DisplayHelper displayHelper)
        {
            speechConfig = SpeechConfig.FromSubscription(serviceKey, endpoint);
            display = displayHelper;
        }

        public async Task StartMicrophoneRecognizer()
        {
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            micRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var stopRecognition = new TaskCompletionSource<int>();

            micRecognizer.Recognizing += (s, e) =>
            {
                display.SetRealtimeMicrophoneTranscription(e.Result.Text);
            };
            micRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    display.PrependTranscription(e.Result.Text);
                    display.SetRealtimeMicrophoneTranscription("");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    display.DisplayError("ERROR: No Match. Speech not recognized.");
                }
            };
            micRecognizer.Canceled += (s, e) =>
            {
                display.DisplayError($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    display.DisplayError($"CANCELED: ErrorCode={e.ErrorCode}; ErrorDetails={e.ErrorDetails}; Reason={e.Reason}");
                }

                stopRecognition.TrySetResult(0);
            };
            micRecognizer.SessionStopped += (s, e) =>
            {
                stopRecognition.TrySetResult(0);
            };

            await micRecognizer.StartContinuousRecognitionAsync();
        }

        public async Task StopMicrophoneRecognizer()
        {
            if (micRecognizer != null)
            {
                await micRecognizer.StopContinuousRecognitionAsync();
            }
        }

        public async Task StartAudioRecognizer(MMDevice selectedDevice)
        {
            loopbackCapture = new WasapiLoopbackCapture(selectedDevice);
            audioInputStream = AudioInputStream.CreatePushStream();
            loopbackCapture.DataAvailable += (s, a) =>
            {
                audioInputStream.Write(ToPCM16(a.Buffer, a.BytesRecorded, loopbackCapture.WaveFormat));
            };

            using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            audioRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var stopRecognition = new TaskCompletionSource<int>();

            audioRecognizer.Recognizing += (s, e) =>
            {
                display.SetRealtimeAudioTranscription(e.Result.Text);
            };
            audioRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    display.PrependTranscription(e.Result.Text);
                    display.SetRealtimeAudioTranscription("");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    display.DisplayError("ERROR: No Match. Speech not recognized.");
                }
            };
            audioRecognizer.Canceled += (s, e) =>
            {
                display.DisplayError($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    display.DisplayError($"CANCELED: ErrorCode={e.ErrorCode}; ErrorDetails={e.ErrorDetails}; Reason={e.Reason}");
                }

                stopRecognition.TrySetResult(0);
            };
            audioRecognizer.SessionStopped += (s, e) =>
            {
                stopRecognition.TrySetResult(0);
            };

            await audioRecognizer.StartContinuousRecognitionAsync();
            loopbackCapture.StartRecording();
        }

        public async Task StopAudioRecognizer()
        {
            if (audioRecognizer != null)
            {
                await audioRecognizer.StopContinuousRecognitionAsync();
                loopbackCapture.StopRecording();
                loopbackCapture.Dispose();
            }
        }

        public byte[] ToPCM16(byte[] inputBuffer, int length, WaveFormat format)
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
