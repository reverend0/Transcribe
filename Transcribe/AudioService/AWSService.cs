using Force.Crc32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Transcribe.AudioService
{
    class AWSService : ITranscriptionService
    {
        private const string Service = "transcribe";
        private const string Path = "/stream-transcription-websocket";
        private const string Scheme = "AWS4";
        private const string Algorithm = "HMAC-SHA256";
        private const string Terminator = "aws4_request";
        private const string HmacSha256 = "HMACSHA256";

        private readonly string _region;
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;
        private readonly DisplayHelper Display;

        private WasapiLoopbackCapture loopbackCapture;
        private ClientWebSocket audioWS;
        private CancellationTokenSource audioCTS;

        public AWSService(string AWSAccessKey, string AWSSecretKey, string AWSRegion, DisplayHelper displayHelper)
        {
            _region = AWSRegion;
            _awsAccessKey = AWSAccessKey;
            _awsSecretKey = AWSSecretKey;

            Display = displayHelper;
        }

        public async Task StartAudioRecognizer(MMDevice selectedDevice)
        {
            audioWS = new ClientWebSocket();
            audioCTS = new CancellationTokenSource();

            await audioWS.ConnectAsync(new Uri(GenerateUrl()), audioCTS.Token);

            loopbackCapture = new WasapiLoopbackCapture(selectedDevice);
            loopbackCapture.DataAvailable += (s, a) =>
            {
                byte[] buffer = AudioUtility.ToPCM16(a.Buffer, a.BytesRecorded, loopbackCapture.WaveFormat);
                //string bufferStr = Encoding.UTF8.GetString(buffer);
                //string bufferStr = Convert.ToBase64String(buffer);
                Dictionary<string, string> headers = new Dictionary<string, string>(2);
                headers.Add(":message-type", "event");
                headers.Add(":event-type", "AudioEvent");
                byte[] payload = AWSEventStreamMarshaller.marshall(headers, buffer);
                /*string json = "{headers: {':message-type': {type: 'string', value: 'event'}, ':event-type': {type: 'string', value: 'AudioEvent'}" +
                              "}, body: " + bufferStr + "}";*/
                Display.PrependTranscription(DateTime.Now + ": " + payload.Length);
                audioWS.SendAsync(payload, WebSocketMessageType.Binary, false, audioCTS.Token);
            };
            loopbackCapture.StartRecording();
        }

        public Task StartMicrophoneRecognizer()
        {
            throw new NotImplementedException();
        }

        public async Task StopAudioRecognizer()
        {
            if (loopbackCapture != null)
            {
                if (audioWS != null && audioWS.State == WebSocketState.Open)
                {
                    audioCTS.CancelAfter(1000);
                    await audioWS.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
                    await audioWS.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                audioWS.Dispose();
                audioWS = null;
                audioCTS.Dispose();
                audioCTS = null;
                loopbackCapture.StopRecording();
                loopbackCapture.Dispose();
            }
        }

        public Task StopMicrophoneRecognizer()
        {
            throw new NotImplementedException();
        }

        public async Task ReadResponse()
        {
            byte[] buffer = new byte[1024];
            MemoryStream result = new();
            WebSocketReceiveResult receiveResult = null;
            do
            {
                try
                {
                    if (audioWS != null && audioCTS != null)
                        receiveResult = await audioWS.ReceiveAsync(buffer, audioCTS.Token);
                }
                catch (WebSocketException ex)
                {
                    Display.DisplayError(ex.Message);
                    break;
                }
                if (receiveResult != null && receiveResult.MessageType != WebSocketMessageType.Close)
                {
                    result.Write(buffer);
                }
            } while (audioWS != null && !receiveResult.EndOfMessage);

            StreamReader reader = new(result);
            string value = reader.ReadToEnd();

            if (value != null && !value.Equals(""))
            {
                Root resultObject = JsonSerializer.Deserialize<Root>(value);
                if (!resultObject.Transcript.Results[0].IsPartial)
                {
                    // Write this to the main history.
                    Display.PrependTranscription(resultObject.Transcript.Results[0].Alternatives[0].Transcript);
                }
                else
                {
                    // Write this to real-time.
                    Display.SetRealtimeAudioTranscription(resultObject.Transcript.Results[0].Alternatives[0].Transcript);
                }
            }

            result.Dispose();
            reader.Dispose();
        }

        private string GenerateUrl()
        {
            var host = $"transcribestreaming.{_region}.amazonaws.com:8443";
            var dateNow = DateTime.UtcNow;
            var dateString = dateNow.ToString("yyyyMMdd");
            var dateTimeString = dateNow.ToString("yyyyMMddTHHmmssZ");
            var credentialScope = $"{dateString}/{_region}/{Service}/{Terminator}";
            var query = GenerateQueryParams(dateTimeString, credentialScope);
            var signature = GetSignature(host, dateString, dateTimeString, credentialScope);
            return $"wss://{host}{Path}?{query}&X-Amz-Signature={signature}";
        }

        private string GenerateQueryParams(string dateTimeString, string credentialScope)
        {
            var credentials = $"{_awsAccessKey}/{credentialScope}";
            var result = new Dictionary<string, string>
            {
                {"X-Amz-Algorithm", "AWS4-HMAC-SHA256"},
                {"X-Amz-Credential", credentials},
                {"X-Amz-Date", dateTimeString},
                {"X-Amz-Expires", "30"},
                {"X-Amz-SignedHeaders", "host"},
                {"language-code", "en-US"},
                {"media-encoding", "pcm"},
                {"sample-rate", "16000"}
            };
            return string.Join("&", result.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
        }


        private string GetSignature(string host, string dateString, string dateTimeString, string credentialScope)
        {
            var canonicalRequest = CanonicalizeRequest(Path, host, dateTimeString, credentialScope);
            var canonicalRequestHashBytes = ComputeHash(canonicalRequest);

            // construct the string to be signed
            var stringToSign = new StringBuilder();
            stringToSign.AppendFormat("{0}-{1}\n{2}\n{3}\n", Scheme, Algorithm, dateTimeString, credentialScope);
            stringToSign.Append(ToHexString(canonicalRequestHashBytes, true));

            var kha = KeyedHashAlgorithm.Create(HmacSha256);
            kha.Key = DeriveSigningKey(HmacSha256, _awsSecretKey, _region, dateString, Service);

            // compute the final signature for the request, place into the result and return to the 
            // user to be embedded in the request as needed
            var signature = kha.ComputeHash(Encoding.UTF8.GetBytes(stringToSign.ToString()));
            var signatureString = ToHexString(signature, true);
            return signatureString;
        }

        private string CanonicalizeRequest(string path, string host, string dateTimeString, string credentialScope)
        {
            var canonicalRequest = new StringBuilder();
            canonicalRequest.AppendFormat("{0}\n", "GET");
            canonicalRequest.AppendFormat("{0}\n", path);
            canonicalRequest.AppendFormat("{0}\n", GenerateQueryParams(dateTimeString, credentialScope));
            canonicalRequest.AppendFormat("{0}\n", $"host:{host}");
            canonicalRequest.AppendFormat("{0}\n", "");
            canonicalRequest.AppendFormat("{0}\n", "host");
            canonicalRequest.Append(ToHexString(ComputeHash(""), true));
            return canonicalRequest.ToString();
        }

        private static string ToHexString(byte[] data, bool lowercase)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString(lowercase ? "x2" : "X2"));
            }
            return sb.ToString();
        }

        private static byte[] DeriveSigningKey(string algorithm, string awsSecretAccessKey, string region, string date, string service)
        {
            char[] ksecret = (Scheme + awsSecretAccessKey).ToCharArray();
            byte[] hashDate = ComputeKeyedHash(algorithm, Encoding.UTF8.GetBytes(ksecret), Encoding.UTF8.GetBytes(date));
            byte[] hashRegion = ComputeKeyedHash(algorithm, hashDate, Encoding.UTF8.GetBytes(region));
            byte[] hashService = ComputeKeyedHash(algorithm, hashRegion, Encoding.UTF8.GetBytes(service));
            return ComputeKeyedHash(algorithm, hashService, Encoding.UTF8.GetBytes(Terminator));
        }

        private static byte[] ComputeKeyedHash(string algorithm, byte[] key, byte[] data)
        {
            var kha = KeyedHashAlgorithm.Create(algorithm);
            kha.Key = key;
            return kha.ComputeHash(data);
        }

        private static byte[] ComputeHash(string data)
        {
            return HashAlgorithm.Create("SHA-256").ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
        public class Item
        {
            public double Confidence { get; set; }
            public string Content { get; set; }
            public double EndTime { get; set; }
            public double StartTime { get; set; }
            public string Type { get; set; }
            public bool VocabularyFilterMatch { get; set; }
        }

        public class Alternative
        {
            public List<Item> Items { get; set; }
            public string Transcript { get; set; }
        }

        public class Result
        {
            public List<Alternative> Alternatives { get; set; }
            public double EndTime { get; set; }
            public bool IsPartial { get; set; }
            public string ResultId { get; set; }
            public double StartTime { get; set; }
        }

        public class Transcript
        {
            public List<Result> Results { get; set; }
        }

        public class Root
        {
            public Transcript Transcript { get; set; }
        }

    }
}
