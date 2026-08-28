using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.W24.S5
{
    /// <summary>
    /// Cross-runtime, typed canonical bytes for the W24 metrics-report self-seal.
    /// JSON spelling is deliberately not part of this encoding: integer and double are
    /// distinct types, object names sort by their strict UTF-8 bytes, and the fixed
    /// domain prefix prevents reuse as an untyped SHA-256 preimage.
    /// </summary>
    public static class W24TypedBinaryCanonicalEncoding
    {
        public const string EncodingName = "w24-typed-binary-v1";
        private static readonly byte[] Domain = Encoding.ASCII.GetBytes("w24-typed-binary-v1\0");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const int MaxDepth = 64;
        private const int MaxNodes = 100000;
        private const int MaxStringBytes = 1048576;
        private const int MaxContainerItems = 100000;

        public static string Hash(JToken value)
        {
            if (value == null) throw new ArgumentNullException("value");
            using (var bytes = new MemoryStream())
            {
                bytes.Write(Domain, 0, Domain.Length);
                var nodes = 0;
                Encode(value, bytes, 0, ref nodes);
                using (var sha = SHA256.Create())
                {
                    return "sha256:" + string.Concat(sha.ComputeHash(bytes.ToArray()).Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
                }
            }
        }

        public static bool Verify(string claimedHash, JToken value)
        {
            return CanonicalHash(claimedHash) && string.Equals(claimedHash, Hash(value), StringComparison.Ordinal);
        }

        private static void Encode(JToken token, Stream output, int depth, ref int nodes)
        {
            if (depth > MaxDepth) throw new InvalidDataException("Typed canonical value exceeds maximum depth.");
            if (++nodes > MaxNodes) throw new InvalidDataException("Typed canonical value exceeds maximum node count.");
            if (token == null || token.Type == JTokenType.Null) { output.WriteByte(0); return; }
            if (token.Type == JTokenType.Boolean) { var boolean = ((JValue)token).Value; if (!(boolean is bool)) throw new InvalidDataException("Typed canonical Boolean token is invalid."); output.WriteByte((bool)boolean ? (byte)2 : (byte)1); return; }
            if (token.Type == JTokenType.Integer) { output.WriteByte(3); WriteBytes(output, StrictAscii(IntegerText((JValue)token))); return; }
            if (token.Type == JTokenType.Float) { var number = ((JValue)token).Value; if (!(number is double)) throw new InvalidDataException("Typed canonical float must be a binary64 double."); output.WriteByte(4); WriteDouble(output, (double)number); return; }
            if (token.Type == JTokenType.String) { output.WriteByte(5); WriteBytes(output, StrictBytes((string)(JValue)token)); return; }
            if (token.Type == JTokenType.Array) { EncodeArray((JArray)token, output, depth, ref nodes); return; }
            if (token.Type == JTokenType.Object) { EncodeObject((JObject)token, output, depth, ref nodes); return; }
            throw new InvalidDataException("Typed canonical encoding rejects JSON token type " + token.Type + ".");
        }

        private static void EncodeArray(JArray array, Stream output, int depth, ref int nodes)
        {
            if (array.Count > MaxContainerItems) throw new InvalidDataException("Typed canonical array has too many items.");
            output.WriteByte(6); WriteU32(output, array.Count);
            foreach (var value in array) Encode(value, output, depth + 1, ref nodes);
        }

        private static void EncodeObject(JObject obj, Stream output, int depth, ref int nodes)
        {
            if (obj.Count > MaxContainerItems) throw new InvalidDataException("Typed canonical object has too many fields.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<KeyValuePair<JProperty, byte[]>>();
            foreach (var property in obj.Properties())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Typed canonical object repeats field " + property.Name + ".");
                entries.Add(new KeyValuePair<JProperty, byte[]>(property, StrictBytes(property.Name)));
            }
            entries.Sort((left, right) => CompareBytes(left.Value, right.Value));
            output.WriteByte(7); WriteU32(output, entries.Count);
            foreach (var entry in entries) { WriteBytes(output, entry.Value); Encode(entry.Key.Value, output, depth + 1, ref nodes); }
        }

        private static string IntegerText(JValue value)
        {
            var raw = value.Value;
            if (raw is BigInteger) return ((BigInteger)raw).ToString(CultureInfo.InvariantCulture);
            switch (Type.GetTypeCode(raw.GetType()))
            {
                case TypeCode.SByte: case TypeCode.Byte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                    return Convert.ToString(raw, CultureInfo.InvariantCulture);
                default: throw new InvalidDataException("Typed canonical integer is not an integral CLR value.");
            }
        }

        private static byte[] StrictAscii(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            if (bytes.Length == 0 || bytes.Any(item => (item < (byte)'0' || item > (byte)'9') && item != (byte)'-')) throw new InvalidDataException("Typed canonical integer is not minimal decimal.");
            return bytes;
        }

        private static byte[] StrictBytes(string text)
        {
            if (text == null) throw new InvalidDataException("Typed canonical strings cannot be null.");
            byte[] bytes;
            try { bytes = StrictUtf8.GetBytes(text); }
            catch (EncoderFallbackException e) { throw new InvalidDataException("Typed canonical string contains a lone surrogate.", e); }
            if (bytes.Length > MaxStringBytes) throw new InvalidDataException("Typed canonical string exceeds maximum UTF-8 byte length.");
            return bytes;
        }

        private static void WriteDouble(Stream output, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Typed canonical double must be finite.");
            var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            for (var shift = 56; shift >= 0; shift -= 8) output.WriteByte((byte)(bits >> shift));
        }

        private static void WriteBytes(Stream output, byte[] value) { WriteU32(output, value.Length); output.Write(value, 0, value.Length); }
        private static void WriteU32(Stream output, int value)
        {
            if (value < 0) throw new InvalidDataException("Typed canonical length/count is invalid.");
            var number = unchecked((uint)value);
            output.WriteByte((byte)(number >> 24)); output.WriteByte((byte)(number >> 16)); output.WriteByte((byte)(number >> 8)); output.WriteByte((byte)number);
        }
        private static int CompareBytes(byte[] left, byte[] right)
        {
            var count = Math.Min(left.Length, right.Length);
            for (var index = 0; index < count; index++) { var compare = left[index].CompareTo(right[index]); if (compare != 0) return compare; }
            return left.Length.CompareTo(right.Length);
        }
        private static bool CanonicalHash(string value) { return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')); }
    }
}
