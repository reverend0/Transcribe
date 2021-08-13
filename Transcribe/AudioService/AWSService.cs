using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public AWSService(string AWSAccessKey, string AWSSecretKey, string AWSRegion)
        {
            _region = AWSRegion;
            _awsAccessKey = AWSAccessKey;
            _awsSecretKey = AWSSecretKey;
        }

        public Task StartAudioRecognizer(MMDevice selectedDevice)
        {
            throw new NotImplementedException();
        }

        public Task StartMicrophoneRecognizer()
        {
            throw new NotImplementedException();
        }

        public Task StopAudioRecognizer()
        {
            throw new NotImplementedException();
        }

        public Task StopMicrophoneRecognizer()
        {
            throw new NotImplementedException();
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
                {"sample-rate", "44100"}
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
    }
}
