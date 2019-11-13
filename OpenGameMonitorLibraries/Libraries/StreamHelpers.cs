using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenGameMonitorLibraries
{
    public static class StreamHelpers
    {
        #region Struct helpers

        public static T ReadStruct<T>(this Stream stream)
        {
            return ReadStruct<T>(stream, Marshal.SizeOf(typeof(T)));
        }

        public static T ReadStruct<T>(this Stream stream, int length)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] data = new byte[length];
            stream.Read(data, 0, data.Length);

            IntPtr ptr = Marshal.AllocHGlobal(length);
            Marshal.Copy(data, 0, ptr, length);
            T structInstance = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);

            return structInstance;
        }

        public static void WriteStruct<T>(this Stream stream, T structToWrite)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            int size = Marshal.SizeOf(structToWrite);
            byte[] data = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structToWrite, ptr, true);
            Marshal.Copy(ptr, data, 0, size);
            Marshal.FreeHGlobal(ptr);

            stream.Write(data, 0, data.Length);
        }
        #endregion

        #region Signed integer helpers
        public static SByte ReadInt8(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return (SByte)stream.ReadByte();
        }

        public static Int16 ReadInt16(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = new byte[2];
            stream.Read(data, 0, 2);
            return BitConverter.ToInt16(data, 0);
        }

        public static Int32 ReadInt32(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = new byte[4];
            stream.Read(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        public static void WriteInt8(this Stream stream, SByte value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            stream.WriteByte((byte)value);
        }

        public static void WriteInt16(this Stream stream, Int16 value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = BitConverter.GetBytes(value);
            stream.Write(data, 0, 2);
        }

        public static void WriteInt32(this Stream stream, Int32 value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = BitConverter.GetBytes(value);
            stream.Write(data, 0, 4);
        }
        #endregion

        #region Unsigned integer helpers
        public static Byte ReadUInt8(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return (byte)stream.ReadByte();
        }

        public static UInt16 ReadUInt16(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = new byte[2];
            stream.Read(data, 0, 2);
            return BitConverter.ToUInt16(data, 0);
        }

        public static UInt32 ReadUInt32(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = new byte[4];
            stream.Read(data, 0, 4);
            return BitConverter.ToUInt32(data, 0);
        }

        public static void WriteUInt8(this Stream stream, byte value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            stream.WriteByte(value);
        }

        public static void WriteUInt16(this Stream stream, UInt16 value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = BitConverter.GetBytes(value);
            stream.Write(data, 0, 2);
        }

        public static void WriteUInt32(this Stream stream, UInt32 value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = BitConverter.GetBytes(value);
            stream.Write(data, 0, 4);
        }

        public static void WriteUInt64(this Stream stream, UInt64 value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] data = BitConverter.GetBytes(value);
            stream.Write(data, 0, 8);
        }
        #endregion

        #region String helpers
        public static char ReadChar8(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return (char)stream.ReadByte();
        }

        public static char ReadChar16(this Stream stream)
        {
            return (char)stream.ReadUInt16();
        }

        public static string ReadAsciiNullTerminatedString(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                char c = (char)stream.ReadByte();
                if (c == 0)
                    return sb.ToString();
                else
                    sb.Append(c);
            }
        }

        public static int WriteAsciiNullTerminatedString(this Stream stream, string data)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = Encoding.ASCII.GetBytes(data);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            return bytes.Length + 1;
        }

        public static string ReadAsciiString(this Stream stream, int length)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = new byte[length];
            stream.Read(bytes, 0, length);
            return Encoding.ASCII.GetString(bytes);
        }

        public static int WriteAsciiString(this Stream stream, string data)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] bytes = Encoding.ASCII.GetBytes(data);
            stream.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
        #endregion

        #region Alignment helpers
        public static void Align(this Stream stream, uint alignment)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            long position = stream.Position;
            long outBy = position % alignment;

            if (outBy == 0)
                return;
            else
            {
                long offset = alignment - outBy;
                stream.Seek(offset, SeekOrigin.Current);
            }
        }
        #endregion

        #region Boolean helpers
        public static bool ReadBoolean(this Stream stream)
        {
            return ReadBoolean(stream, 1);
        }

        public static bool ReadBoolean(this Stream stream, int length)
        {
            byte[] data = new byte[length];
            switch (length)
            {
                case 1: return data[0] != 0;
                case 2: return BitConverter.ToUInt16(data, 0) != 0;
                case 4: return BitConverter.ToUInt32(data, 0) != 0;
            }

            throw new NotImplementedException();
        }
        #endregion

        #region StreamReader helpers
        public static char ReadChar(this StreamReader sr)
        {
            if (sr == null) throw new ArgumentNullException(nameof(sr));
            return (char)sr.Read();
        }

        public static char PeekChar(this StreamReader sr)
        {
            if (sr == null) throw new ArgumentNullException(nameof(sr));
            return (char)sr.Peek();
        }
        #endregion

        /// <summary>
        /// Copy to target stream, while flushing the data after every operation.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination stream.</param>
        /// <param name="bufferSize">The copy buffer size.</param>
        /// <param name="flush">Indicates whether the data should be flushed after every operation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, bool flush, CancellationToken cancellationToken)
        {
            if (!flush)
            {
                await source.CopyToAsync(destination, bufferSize, cancellationToken);
                return;
            }

            var buffer = new byte[bufferSize];
            int readCount;
            while ((readCount = await source.ReadAsync(buffer, 0, bufferSize, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, readCount, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
