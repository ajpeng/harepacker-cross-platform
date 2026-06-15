using MapleLib;
using MapleLib.ClientLib;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace UnitTest_WzFile {

    [TestClass]
    public class UnitTest_MapleLib {

        public UnitTest_MapleLib() {
        }

        /// <summary>
        /// Test CCrc32::GetCrc32 calculation
        /// </summary>
        [TestMethod]
        public void TestCrcCalculation() {
            int useVersion = 200;

            uint crc_firstRun = CCrc32.GetCrc32(useVersion, 0, false, false);
            Assert.IsTrue(crc_firstRun == 2384409922, "Expected value = (2,384,409,922), got {0}", crc_firstRun.ToString());

            uint crc = CWvsPhysicalSpace2D.GetConstantCRC(useVersion);
            Assert.IsTrue(crc == 1696968404, "Expected value = (crc = 1,696,968,404), got {0}", crc.ToString());
        }

        /// <summary>
        /// Bgra32 (Small Icon): 27x30 pixels, with alpha, few colors. Represents a typical small icon that needs alpha channel preservation.
        /// Bgr565(Small Icon) : 27x30 pixels, no alpha, moderate color count.Suitable for small images without alpha that need decent color representation.
        /// Bgra4444(Small Icon): 27x30 pixels, with alpha, few colors.Good for small icons with simple color schemes that still need alpha.
        /// DXT5 (Large Texture): 128x128 pixels, with alpha, gradients, many colors. Ideal for larger textures with smooth alpha transitions.
        /// DXT3 (Large Texture): 128x128 pixels, with alpha, no gradients, moderate colors. Suitable for larger textures with sharp alpha transitions.
        /// Bgra32 (Large Texture): 128x128 pixels, no alpha, gradients, many colors. Used for larger textures that need full color preservation without alpha.
        /// </summary>
        [TestMethod]
        public void TestImageSurfaceFormatDetection() {
            string[] imageFiles = Directory.GetFiles("Assets/Images", "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => new[] { ".png", ".jpg", ".bmp" }.Contains(Path.GetExtension(file).ToLower()))
            .ToArray();

            Assert.IsTrue(imageFiles.Length > 0, "No image files found in the Assets/Images folder.");

            foreach (string imagePath in imageFiles) {
                using SKBitmap decoded = SKBitmap.Decode(imagePath);
                // Ensure BGRA8888 layout so pixel byte order matches ImageFormatDetector expectations
                using SKBitmap bitmap = decoded.ColorType == SKColorType.Bgra8888
                    ? decoded
                    : decoded.Copy(SKColorType.Bgra8888);

                int width = bitmap.Width;
                int height = bitmap.Height;
                int byteCount = bitmap.RowBytes * height;
                byte[] argbData = new byte[byteCount];
                Marshal.Copy(bitmap.GetPixels(), argbData, 0, byteCount);

                WzSurfaceFormat detectedFormat = ImageFormatDetector.DetermineTextureFormat(argbData, width, height);
                var (uniqueRgbColors, uniqueAlphaValues, hasAlpha, hasPartialAlpha, maxAlpha, avgAlphaGradient, alphaVariance, isGrayscale) = ImageFormatDetector.AnalyzeImageData(argbData, width, height);
                bool isDxtCompressionCandidate = ImageFormatDetector.IsDxtCompressionCandidate(width, height);

                Debug.WriteLine($"Image: {Path.GetFileName(imagePath)}");
                Debug.WriteLine($"Dimensions: {width}x{height}");
                Debug.WriteLine($"Total pixels: {width * height}");
                Debug.WriteLine($"Has Alpha: {hasAlpha}");
                Debug.WriteLine($"Has Partial Alpha: {hasPartialAlpha}");
                Debug.WriteLine($"Unique RGB Color: {uniqueRgbColors}");
                Debug.WriteLine($"Unique Alpha Values: {uniqueAlphaValues}");
                Debug.WriteLine($"Max Alpha: {maxAlpha}");
                Debug.WriteLine($"Avg Alpha Gradient: {avgAlphaGradient}");
                Debug.WriteLine($"Alpha Variance: {alphaVariance}");
                Debug.WriteLine($"Is Grayscale: {isGrayscale}");
                Debug.WriteLine($"IsDxtCompressionCandidate: {isDxtCompressionCandidate}");
                Debug.WriteLine($"Detected Format: {detectedFormat}");

                WzSurfaceFormat expectedFormat = GetExpectedFormat(imagePath);

                Debug.WriteLine($"Expected: {expectedFormat.ToString()}");
                Debug.WriteLine("");

                Assert.AreEqual(expectedFormat, detectedFormat,
                    $"Incorrect format detected for {Path.GetFileName(imagePath)}. " +
                    $"Expected: {expectedFormat}, Detected: {detectedFormat}");
            }
        }

        private WzSurfaceFormat GetExpectedFormat(string imagePath) {
            string fileName = Path.GetFileNameWithoutExtension(imagePath).ToLower();
            if (fileName.StartsWith("dxt5")) {
                return WzSurfaceFormat.Dxt5;
            }
            else if (fileName.StartsWith("dxt3")) {
                return WzSurfaceFormat.Dxt3;
            }
            else if (fileName.StartsWith("bgra32") || fileName.StartsWith("bga32")) {
                return WzSurfaceFormat.Bgra32;
            }
            else if (fileName.StartsWith("bgr565")) {
                return WzSurfaceFormat.Bgr565;
            }
            else if (fileName.StartsWith("bgra4444"))
                return WzSurfaceFormat.Bgra4444;

            // Default case
            return WzSurfaceFormat.Color;
        }

    }
}
