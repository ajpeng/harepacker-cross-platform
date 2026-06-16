using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RealESRGAN_AI_Upscale {

    /// <summary>
    /// https://github.com/xinntao/Real-ESRGAN
    /// https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
    /// </summary>
    public class EsrganNcnn {

        private static readonly string BIN_FOLDER = Path.Combine("RealESRGAN");

        private static string ExecName =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "realesrgan-ncnn-vulkan.exe"
                : "realesrgan-ncnn-vulkan";

        /// <summary>
        /// Run the Real-ESRGAN ncnn-vulkan binary to upscale images.
        /// </summary>
        public static async Task Run(string in_pathFolder, string out_pathFolder, int upscaleRatio) {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BIN_FOLDER);
            string execPath = Path.Combine(folderPath, ExecName);

            if (!File.Exists(execPath)) {
                Console.WriteLine($"[EsrganNcnn] Binary not found: {execPath}");
                return;
            }

            string args = $"-i \"{in_pathFolder}\" -o \"{out_pathFolder}\" -s {upscaleRatio} -m \"models\"";
            Console.WriteLine($"[EsrganNcnn] {execPath} {args}");

            using var proc = new Process();
            proc.StartInfo.FileName = execPath;
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.WorkingDirectory = folderPath;

            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("[NCNN] " + e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine("[NCNN] " + e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            while (!proc.HasExited)
                await Task.Delay(50);
        }
    }
}
