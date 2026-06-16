/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using SkiaSharp;

namespace HaCreator.Properties
{
    internal static class Resources
    {
        public static string EnterValidInput => "Please enter valid input";
        public static string Initialization_Error_MSDirectoryNotExist => "The MapleStory directory {0} provided does not exist.";
        public static string Warning => "Warning";

        private static SKBitmap? _placeholder;
        public static SKBitmap placeholder => _placeholder ??= new SKBitmap(1, 1);
    }
}
