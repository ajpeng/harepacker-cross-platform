using SkiaSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace HaSharedLibrary.SharpApng
{
    /// <summary>
    /// Pure C# APNG encoder. Replaces the Windows-only apng64/apng32.dll native wrapper.
    /// APNG spec: https://wiki.mozilla.org/APNG_Specification
    /// </summary>
    internal static class ApngWriter
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static void Write(string path, IReadOnlyList<SharpApngFrame> frames, bool firstFrameHidden)
        {
            if (frames.Count == 0) throw new ArgumentException("No frames to write.", nameof(frames));

            // Encode each frame to PNG bytes and extract IDAT data + dimensions
            var frameData = new (byte[] idat, int width, int height)[frames.Count];
            int maxW = 0, maxH = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                var (idat, w, h) = EncodeToPngIdat(frames[i].Bitmap);
                frameData[i] = (idat, w, h);
                if (w > maxW) maxW = w;
                if (h > maxH) maxH = h;
            }

            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);

            // PNG signature
            bw.Write(PngSignature);

            // IHDR — use dimensions of first frame (common canvas)
            WriteChunk(bw, "IHDR", BuildIHDR(maxW, maxH));

            // acTL — animation control
            int animFrames = firstFrameHidden ? frames.Count - 1 : frames.Count;
            WriteChunk(bw, "acTL", BuildAcTL(animFrames, 0));

            int seq = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var (idat, fw, fh) = frameData[i];

                bool isAnimationFrame = !firstFrameHidden || i > 0;
                if (isAnimationFrame)
                {
                    WriteChunk(bw, "fcTL", BuildFcTL(seq++, fw, fh, 0, 0,
                        (ushort)frame.DelayNum, (ushort)frame.DelayDen, 1, 0));
                }

                if (i == 0)
                {
                    // First frame goes in IDAT (required by APNG spec for fallback display)
                    WriteRawChunk(bw, "IDAT", idat);
                }
                else
                {
                    // Subsequent frames go in fdAT with a sequence number prefix
                    var fdatData = new byte[4 + idat.Length];
                    BinaryPrimitives.WriteUInt32BigEndian(fdatData, (uint)seq++);
                    idat.CopyTo(fdatData, 4);
                    WriteChunk(bw, "fdAT", fdatData);
                }
            }

            // IEND
            WriteChunk(bw, "IEND", Array.Empty<byte>());
        }

        /// <summary>
        /// Encodes an SKBitmap to PNG and extracts the concatenated IDAT payload bytes.
        /// </summary>
        private static (byte[] idat, int width, int height) EncodeToPngIdat(SKBitmap bmp)
        {
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] pngBytes = data.ToArray();
            var idat = ExtractIdatPayload(pngBytes);
            return (idat, bmp.Width, bmp.Height);
        }

        /// <summary>
        /// Parses a PNG byte array and concatenates all IDAT chunk payloads into one buffer.
        /// </summary>
        private static byte[] ExtractIdatPayload(byte[] png)
        {
            var result = new List<byte[]>();
            int pos = 8; // skip PNG signature
            while (pos < png.Length)
            {
                if (pos + 8 > png.Length) break;
                int len = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos, 4));
                string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
                if (type == "IDAT" && len > 0)
                {
                    var chunk = new byte[len];
                    Array.Copy(png, pos + 8, chunk, 0, len);
                    result.Add(chunk);
                }
                pos += 4 + 4 + len + 4; // len + type + data + crc
                if (type == "IEND") break;
            }

            // Concatenate all IDAT payloads
            int total = 0;
            foreach (var b in result) total += b.Length;
            var combined = new byte[total];
            int off = 0;
            foreach (var b in result) { b.CopyTo(combined, off); off += b.Length; }
            return combined;
        }

        private static byte[] BuildIHDR(int width, int height)
        {
            var b = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(b, (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(4), (uint)height);
            b[8] = 8;   // bit depth
            b[9] = 6;   // color type: RGBA
            b[10] = 0;  // compression
            b[11] = 0;  // filter
            b[12] = 0;  // interlace
            return b;
        }

        private static byte[] BuildAcTL(int numFrames, int numPlays)
        {
            var b = new byte[8];
            BinaryPrimitives.WriteUInt32BigEndian(b, (uint)numFrames);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(4), (uint)numPlays);
            return b;
        }

        private static byte[] BuildFcTL(int seq, int width, int height,
            int xOff, int yOff, ushort delayNum, ushort delayDen,
            byte disposeOp, byte blendOp)
        {
            var b = new byte[26];
            BinaryPrimitives.WriteUInt32BigEndian(b, (uint)seq);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(4), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8), (uint)height);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(12), (uint)xOff);
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16), (uint)yOff);
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(20), delayNum);
            BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(22), delayDen);
            b[24] = disposeOp;
            b[25] = blendOp;
            return b;
        }

        private static void WriteChunk(BinaryWriter bw, string type, byte[] data)
        {
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            bw.Write(BinaryPrimitives.ReverseEndianness((uint)data.Length));
            bw.Write(typeBytes);
            bw.Write(data);
            uint crc = Crc32(typeBytes, data);
            bw.Write(BinaryPrimitives.ReverseEndianness(crc));
        }

        /// <summary>
        /// Writes a chunk whose data is already in a raw byte array (no extra copy).
        /// </summary>
        private static void WriteRawChunk(BinaryWriter bw, string type, byte[] payload)
        {
            WriteChunk(bw, type, payload);
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in type) crc = UpdateCrc(crc, b);
            foreach (byte b in data) crc = UpdateCrc(crc, b);
            return crc ^ 0xFFFFFFFF;
        }

        private static uint UpdateCrc(uint crc, byte b)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (0xEDB88320u ^ (crc >> 1)) : (crc >> 1);
            return crc;
        }
    }
}
