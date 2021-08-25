using Force.Crc32;
using System;
using System.Collections.Generic;
using System.Text;

namespace Transcribe.AudioService
{
    class AWSEventStreamMarshaller
    {
        private static byte _string = 7;
        public static byte[] marshall(Dictionary<string, string> rawHeaders, byte[] body)
        {
            byte[] headers = headerMarshall(rawHeaders);
            int length = headers.Length + body.Length + 16;

            byte[] output = new byte[length];

            byte[] payloadLength = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian) Array.Reverse(payloadLength);
            System.Buffer.BlockCopy(payloadLength, 0, output, 0, 4);

            byte[] headerLength = BitConverter.GetBytes(headers.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(headerLength);
            System.Buffer.BlockCopy(headerLength, 0, output, 4, headerLength.Length);

            byte[] crc = BitConverter.GetBytes(Crc32Algorithm.Compute(output, 0, 8));
            if (BitConverter.IsLittleEndian) Array.Reverse(crc);
            System.Buffer.BlockCopy(crc, 0, output, 8, crc.Length);

            System.Buffer.BlockCopy(headers, 0, output, 12, headers.Length);

            if (BitConverter.IsLittleEndian) Array.Reverse(body);
            System.Buffer.BlockCopy(body, 0, output, headers.Length + 12, body.Length);

            crc = BitConverter.GetBytes(Crc32Algorithm.Compute(output, 0, output.Length - 4));
            if (BitConverter.IsLittleEndian) Array.Reverse(crc);
            System.Buffer.BlockCopy(crc, 0, output, length - 4, crc.Length);

            return output;
        }

        private static byte[] headerMarshall(Dictionary<string, string> rawHeaders)
        {
            byte[] results = new byte[0];
            foreach (KeyValuePair<string, string> entry in rawHeaders)
            {
                byte[] pair = keyValueMarshall(entry);
                byte[] temp = new byte[results.Length + pair.Length];
                System.Buffer.BlockCopy(results, 0, temp, 0, results.Length);
                System.Buffer.BlockCopy(pair, 0, temp, results.Length, pair.Length);
                results = temp;
            }
            return results;
        }

        private static byte[] keyValueMarshall(KeyValuePair<string, string> entry)
        {
            byte[] key = UTF8Encoding.UTF8.GetBytes(entry.Key);
            byte[] value = UTF8Encoding.UTF8.GetBytes(entry.Value);

            byte[] temp = new byte[key.Length + value.Length + 4];
            temp.SetValue((byte) key.Length, 0);

            if (BitConverter.IsLittleEndian) Array.Reverse(key);
            System.Buffer.BlockCopy(key, 0, temp, 1, key.Length);

            temp.SetValue(_string, key.Length + 1);

            byte[] valueLength = BitConverter.GetBytes((ushort)value.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(valueLength);
            System.Buffer.BlockCopy(valueLength, 0, temp, key.Length + 2, valueLength.Length);

            if (BitConverter.IsLittleEndian) Array.Reverse(value);
            System.Buffer.BlockCopy(value, 0, temp, key.Length + 4, value.Length);

            return temp;
        }

        enum HEADER_VALUE_TYPE
        {
            _boolTrue = 0,
            _boolFalse,
            _byte,
            _short,
            _integer,
            _long,
            _byteArray,
            _string,
            _timestamp,
            _uuid,
        }
    }
}
