using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace HaRepacker
{
    public static class SavedFolderBrowser
    {
        public static async Task<string> ShowAsync(Window owner, string description)
        {
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = description,
                AllowMultiple = false,
                SuggestedStartLocation = await TryGetSuggestedFolder(owner)
            });

            if (folders.Count == 0) return "";

            string path = folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
            if (Program.ConfigurationManager != null)
                Program.ConfigurationManager.ApplicationSettings.LastBrowserPath = path;
            return path;
        }

        private static async Task<IStorageFolder?> TryGetSuggestedFolder(Window owner)
        {
            string? last = Program.ConfigurationManager?.ApplicationSettings?.LastBrowserPath;
            if (string.IsNullOrEmpty(last)) return null;
            try { return await owner.StorageProvider.TryGetFolderFromPathAsync(last); }
            catch { return null; }
        }
    }
}
