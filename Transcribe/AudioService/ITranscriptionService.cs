using NAudio.CoreAudioApi;
using System.Threading.Tasks;

namespace Transcribe.AudioService
{
    interface ITranscriptionService
    {
        public Task StartMicrophoneRecognizer();
        public Task StopMicrophoneRecognizer();
        public Task StartAudioRecognizer(MMDevice selectedDevice);
        public Task StopAudioRecognizer();
    }
}
