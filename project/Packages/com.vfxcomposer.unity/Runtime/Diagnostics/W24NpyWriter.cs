using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VFXComposer.W24
{
    /// <summary>Small dependency-free NumPy v1.0 writer for formal diagnostic arrays only.</summary>
    public static class W24NpyWriter
    {
        public static byte[] EncodeUInt32(uint[] values, int width, int height) { return Encode(values, width, height, "<u4", BitConverter.GetBytes); }
        public static byte[] EncodeBinaryUInt8(byte[] values, int width, int height)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            foreach (var value in values) if (value != 0 && value != 255) throw new ArgumentException("W24 binary mask diagnostics must contain only 0 or 255.", nameof(values));
            return Encode(values, width, height, "|u1", value => new[] { value });
        }
        public static byte[] EncodeFloat32(float[] values, int width, int height)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            foreach (var value in values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("W24 NPY float diagnostics must be finite.", nameof(values));
            return Encode(values, width, height, "<f4", BitConverter.GetBytes);
        }

        /// <summary>
        /// Encodes a finite, C-contiguous float32 image.  Formal color diagnostics use exactly
        /// three (linear RGB) or four (linear RGBA) channels; scalar depth remains on the
        /// two-dimensional overload above.
        /// </summary>
        public static byte[] EncodeFloat32(float[] values, int width, int height, int channels)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (channels != 3 && channels != 4) throw new ArgumentOutOfRangeException(nameof(channels), "W24 color diagnostic arrays require exactly 3 or 4 channels.");
            foreach (var value in values) if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("W24 NPY float diagnostics must be finite.", nameof(values));
            return Encode(values, width, height, channels, "<f4", BitConverter.GetBytes);
        }

        private static byte[] Encode<T>(T[] values, int width, int height, string descriptor, Func<T, byte[]> convert)
        {
            return Encode(values, width, height, 1, descriptor, convert);
        }

        private static byte[] Encode<T>(T[] values, int width, int height, int channels, string descriptor, Func<T, byte[]> convert)
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("W24 NPY diagnostics require a little-endian host.");
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (width <= 0 || height <= 0 || channels <= 0 || values.Length != checked(width * height * channels)) throw new ArgumentException("W24 NPY dimensions must match the supplied array.");
            var shape = "(" + height.ToString(CultureInfo.InvariantCulture) + ", " + width.ToString(CultureInfo.InvariantCulture) + (channels == 1 ? ")" : ", " + channels.ToString(CultureInfo.InvariantCulture) + ")");
            var header = "{'descr': '" + descriptor + "', 'fortran_order': False, 'shape': " + shape + ", }";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            var padding = 16 - ((10 + headerBytes.Length + 1) % 16);
            if (padding == 16) padding = 0;
            var finalHeader = Encoding.ASCII.GetBytes(header + new string(' ', padding) + "\n");
            if (finalHeader.Length > UInt16.MaxValue) throw new InvalidOperationException("W24 NPY v1 header is too large.");
            using (var stream = new MemoryStream(10 + finalHeader.Length + values.Length * 4))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write((byte)0x93); writer.Write(Encoding.ASCII.GetBytes("NUMPY")); writer.Write((byte)1); writer.Write((byte)0); writer.Write((ushort)finalHeader.Length); writer.Write(finalHeader);
                foreach (var value in values) writer.Write(convert(value));
                writer.Flush(); return stream.ToArray();
            }
        }
    }
}
