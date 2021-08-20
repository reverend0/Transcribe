using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading.Tasks;
using Transcribe.AudioService;

namespace Transcribe
{
    class AzureService : ITranscriptionService
    {
        private readonly SpeechConfig speechConfig;
        private readonly DisplayHelper display;

        private SpeechRecognizer micRecognizer;
        private SpeechRecognizer audioRecognizer;
        private PushAudioInputStream audioInputStream;
        private WasapiLoopbackCapture loopbackCapture;

        public AzureService(string serviceKey, string endpoint, DisplayHelper displayHelper, bool filterProfanity)
        {
            speechConfig = SpeechConfig.FromSubscription(serviceKey, endpoint);
            if (filterProfanity)
            {
                speechConfig.SetProfanity(ProfanityOption.Masked);
            }
            else
            {
                speechConfig.SetProfanity(ProfanityOption.Raw);
            }
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
                audioInputStream.Write(AudioUtility.ToPCM16(a.Buffer, a.BytesRecorded, loopbackCapture.WaveFormat));
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

        public Task ReadResponse()
        {
            return null;
        }
    }
}
