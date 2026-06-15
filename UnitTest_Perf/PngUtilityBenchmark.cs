using BenchmarkDotNet.Attributes;
using MapleLib.Helpers;
using SkiaSharp;

namespace UnitTest_Perf
{
    public class PngUtilityBenchmark
    {
        private SKBitmap bmp;
        private byte[] dxt3Data;
        private byte[] dxt5Data;

        [GlobalSetup]
        public void Setup()
        {
            bmp = new SKBitmap(128, 128, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    bmp.SetPixel(x, y, new SKColor((byte)(x * 8 % 256), (byte)(y * 8 % 256), (byte)((x + y) % 256), (byte)(128 + x % 128)));
            dxt3Data = (byte[])typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            dxt5Data = (byte[])typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
        }

        [Benchmark(Description = "DXT3 Decode 128x128")]
        public byte DXT3_Decode()
        {
            using var bmpOut = new SKBitmap(128, 128, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bmpData = WzBitmapData.FromSKBitmap(bmpOut);
            PngUtility.DecompressImageDXT3(dxt3Data, 128, 128, bmpData);
            return bmpOut.GetPixel(0, 0).Alpha;
        }

        [Benchmark(Description = "DXT5 Decode 128x128")]
        public byte DXT5_Decode()
        {
            using var bmpOut = new SKBitmap(128, 128, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bmpData = WzBitmapData.FromSKBitmap(bmpOut);
            PngUtility.DecompressImageDXT5(dxt5Data, 128, 128, bmpData);
            return bmpOut.GetPixel(0, 0).Alpha;
        }

        [Benchmark(Description = "DXT3 Encode 128x128")]
        public byte DXT3_Encode()
        {
            byte[] encoded = (byte[])typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            return encoded[0];
        }

        [Benchmark(Description = "DXT5 Encode 128x128")]
        public byte DXT5_Encode()
        {
            byte[] encoded = (byte[])typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            return encoded[0];
        }
    }
}
