using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech.Audio;

namespace Transcribe
{
    class AudioSet : PullAudioInputStreamCallback
    {
        private MemoryStream audioStream;

        public AudioSet(MemoryStream stream)
        {
            this.audioStream = stream;
        }

        public override int Read(byte[] dataBuffer, uint size)
        {
            return this.Read(dataBuffer, 0, dataBuffer.Length);
        }

        private int Read(byte[] buffer, int offset, int count)
        {
            return audioStream.Read(buffer, offset, count);
        }
    }
}
