namespace Transcribe
{
    class DisplayHelper
    {
        // Temporary Storage for Transcription that is fed to the display elements
        private Transcriber transcriber;
        private Transcriber rtAudioTranscriber;
        private Transcriber rtMicTranscriber;
        private Transcriber error;

        public DisplayHelper()
        {
            transcriber = new Transcriber();
            rtAudioTranscriber = new Transcriber();
            rtMicTranscriber = new Transcriber();
            error = new Transcriber();
        }

        public void PrependTranscription(string value)
        {
            transcriber.Prepend(value);
        }

        public string GetTranscription()
        {
            return transcriber.Get();
        }

        public void SetRealtimeMicrophoneTranscription(string value)
        {
            rtMicTranscriber.Set(value);
        }

        public string GetRealtimeMic()
        {
            return rtMicTranscriber.Get();
        }

        public void SetRealtimeAudioTranscription(string value)
        {
            rtAudioTranscriber.Set(value);
        }

        public string GetRealtimeAudio()
        {
            return rtAudioTranscriber.Get();
        }

        public void DisplayError(string value)
        {
            error.Set(value);
        }

        public string GetError()
        {
            return error.Get();
        }

        public void Clear()
        {
            rtAudioTranscriber.Clear();
            rtMicTranscriber.Clear();
            transcriber.Clear();
        }
    }
}
