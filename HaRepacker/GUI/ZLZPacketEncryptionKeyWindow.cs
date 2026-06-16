using Avalonia.Controls;
using Avalonia.Layout;
using HaSharedLibrary.SystemInterop;
using HaSharedLibrary.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HaRepacker.GUI
{
    /// <summary>
    /// Extracts AES encryption keys from ZLZ.dll / ZLZ64.dll (MapleStory packet encryption DLLs).
    /// Key extraction is Windows-only (requires kernel32 LoadLibrary + memory scanning).
    /// Credits: http://forum.ragezone.com/f921/release-gms-key-retriever-895646/
    /// </summary>
    public class ZLZPacketEncryptionKeyWindow : Window
    {
        private readonly TextBox _odinBox;
        private readonly TextBox _othersBox;

        public ZLZPacketEncryptionKeyWindow()
        {
            Title = "ZLZ Packet Encryption Key";
            Width = 680;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = true;

            _odinBox = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            _othersBox = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,*,Auto") };

            void AddRow(int row, string label, TextBox box)
            {
                var lbl = new TextBlock { Text = label, Margin = new Avalonia.Thickness(4, 6, 4, 2) };
                Grid.SetRow(lbl, row);
                grid.Children.Add(lbl);
                Grid.SetRow(box, row + 1);
                grid.Children.Add(box);
            }

            AddRow(0, "OdinMS format:", _odinBox);
            AddRow(2, "MapleShark / combined format:", _othersBox);

            var closeBtn = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(4) };
            closeBtn.Click += (_, _) => Close();
            Grid.SetRow(closeBtn, 4);
            grid.Children.Add(closeBtn);

            Content = new Border { Padding = new Avalonia.Thickness(8), Child = grid };
        }

        public bool LoadZLZ(string filePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                Warning.Error("ZLZ key extraction is only supported on Windows.");
                return false;
            }

            bool is64 = Path.GetFileName(filePath).Equals("ZLZ64.dll", StringComparison.OrdinalIgnoreCase);
            return is64 ? OpenZLZDllFile_64Bit(filePath) : OpenZLZDllFile_32Bit(filePath);
        }

        private bool OpenZLZDllFile_64Bit(string filePath)
        {
            var fileinfo = new FileInfo(filePath);
            kernel32.SetDllDirectory(fileinfo.Directory!.FullName);

            const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;
            IntPtr module = kernel32.LoadLibraryEx(fileinfo.FullName, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);

            if (module == IntPtr.Zero)
            {
                uint lastError = kernel32.GetLastError();
                Warning.Error($"Unable to load DLL. kernel32 GetLastError(): {lastError}");
                return false;
            }

            bool bError = true;
            IntPtr func_setEncryptionKey = IntPtr.Zero;
            IntPtr encryptionKeyLoc = IntPtr.Zero;
            try
            {
                var currentProcess = Process.GetCurrentProcess().Handle;

                const string SEARCH_PATTERN_SET_ENC = "83 3D ?? ?? ?? 00 00 0F 85 ?? 01 00 00 C7 ?? ?? ?? ?? ?? ??";
                func_setEncryptionKey = MemoryScannerHelper.ScanCurrentProcessMemory(
                    currentProcess, module.ToInt64(), module.ToInt64() + 0x10000, SEARCH_PATTERN_SET_ENC);

                const string SEARCH_PATTERN_ENC_KEY = "13 00 00 00 52 00 00 00 2A 00 00 00";
                encryptionKeyLoc = MemoryScannerHelper.ScanCurrentProcessMemory(
                    currentProcess, module.ToInt64(), module.ToInt64() + 0x100000, SEARCH_PATTERN_ENC_KEY);

                if (func_setEncryptionKey != IntPtr.Zero)
                {
                    var method = Marshal.GetDelegateForFunctionPointer(func_setEncryptionKey, typeof(ZLZGenerateKey.GenerateKey)) as ZLZGenerateKey.GenerateKey;
                    method!();
                    ShowKey_64Bit(module, (int)(encryptionKeyLoc.ToInt64() - module.ToInt64()));
                    bError = false;
                }
            }
            catch (Exception ex)
            {
                Warning.Error($"Invalid KeyGen position. This version of MapleStory may be unsupported.\n{ex}");
            }
            finally
            {
                kernel32.FreeLibrary(module);
            }

            if (bError)
                Warning.Error($"Invalid KeyGen position.\nfunc_setEncryptionKey={func_setEncryptionKey:X8}, encryptionKeyLoc={encryptionKeyLoc:X8}");

            return !bError;
        }

        private void ShowKey_64Bit(IntPtr module, int baseKeyPosition)
        {
            var sb = new StringBuilder();
            var sb2 = new StringBuilder();
            var sbCombined = new StringBuilder();
            for (int i = 0; i < 128; i += 4)
            {
                IntPtr addr = (IntPtr)(module.ToInt64() + baseKeyPosition + i);
                byte val = Marshal.ReadByte(addr);
                sb.Append($"(byte) 0x{val:X}, ");
                sb2.Append($"0x{val:X2}, ");
                sbCombined.Append($"{val:X2}");
            }
            _odinBox.Text = sb.ToString().TrimEnd(',', ' ');
            _othersBox.Text = sb2.ToString().TrimEnd(',', ' ') + Environment.NewLine + Environment.NewLine + sbCombined;
        }

        private bool OpenZLZDllFile_32Bit(string filePath)
        {
            var fileinfo = new FileInfo(filePath);
            kernel32.SetDllDirectory(fileinfo.Directory!.FullName);
            IntPtr module = kernel32.LoadLibrary(fileinfo.FullName);
            if (module == IntPtr.Zero)
            {
                uint lastError = kernel32.GetLastError();
                Warning.Error($"Unable to load DLL. kernel32 GetLastError(): {lastError}");
                return false;
            }
            try
            {
                IntPtr functionAddress = (IntPtr)(module.ToInt32() + 0x1340);
                var method = Marshal.GetDelegateForFunctionPointer(functionAddress, typeof(ZLZGenerateKey.GenerateKey)) as ZLZGenerateKey.GenerateKey;
                method!();
                ShowKey_32Bit(module, 0x14020);
                return true;
            }
            catch (Exception ex)
            {
                Warning.Error($"Invalid KeyGen position. This version of MapleStory may be unsupported.\n{ex}");
                return false;
            }
            finally
            {
                kernel32.FreeLibrary(module);
            }
        }

        private void ShowKey_32Bit(IntPtr module, int baseKeyPosition)
        {
            var sb = new StringBuilder();
            var sb2 = new StringBuilder();
            var sbCombined = new StringBuilder();
            for (int i = 0; i < 128; i += 4)
            {
                IntPtr addr = (IntPtr)(module.ToInt32() + baseKeyPosition + i);
                byte val = Marshal.ReadByte(addr);
                if (i % 16 == 0)
                    sb.Append($"(byte) 0x{Marshal.ReadInt32(addr) & 0xFF:X}, (byte) 0x00, (byte) 0x00, (byte) 0x00, ");
                sb2.Append($"0x{val:X2}, ");
                sbCombined.Append($"{val:X2}");
            }
            _odinBox.Text = sb.ToString().TrimEnd(',', ' ');
            _othersBox.Text = sb2.ToString().TrimEnd(',', ' ') + Environment.NewLine + Environment.NewLine + sbCombined;
        }
    }
}
