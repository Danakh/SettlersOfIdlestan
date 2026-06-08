using SettlersOfIdlestanSkia.Services;
using Microsoft.Maui.Storage;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
#endif

namespace SettlersOfIdlestanDesktop.Services;

public class DesktopFileSystemService : IFileSystemService
{
    private static string GetSavesDirectory()
    {
        var exeDir = AppContext.BaseDirectory;
        var savesDir = Path.Combine(exeDir, "saves");
        if (!Directory.Exists(savesDir))
            Directory.CreateDirectory(savesDir);
        return savesDir;
    }

    private static string GetAutoSavePath() => Path.Combine(GetSavesDirectory(), "autosave.json");

    public async Task SaveText(string fileName, string content)
    {
#if WINDOWS
        var window = (MauiWinUIWindow?)App.Current?.Windows[0].Handler.PlatformView;
        if (window != null)
        {
            nint hwnd = window.WindowHandle;
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeChoices.Add("Fichier JSON", new List<string> { ".json" });
            picker.SuggestedFileName = fileName;
            picker.DefaultFileExtension = ".json";
            picker.SettingsIdentifier = "SettlersOfIdlestanSave";
            try
            {
                picker.SuggestedSaveFile = await StorageFile.GetFileFromPathAsync(Path.Combine(GetSavesDirectory(), fileName));
            }
            catch {}
            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await FileIO.WriteTextAsync(file, content);
            }
        }
#else
        var path = Path.Combine(GetSavesDirectory(), fileName);
        File.WriteAllText(path, content);
#endif
    }

    public async Task<string?> LoadText(string fileName)
    {
        var savesDir = GetSavesDirectory();
        var options = new PickOptions
        {
            PickerTitle = "Charger une sauvegarde",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.MacCatalyst, new[] { ".json" } },
                { DevicePlatform.Android, new[] { "application/json" } },
                { DevicePlatform.iOS, new[] { "public.json" } },
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        if (result != null && File.Exists(result.FullPath))
        {
            return File.ReadAllText(result.FullPath);
        }
        return null;
    }

    public async Task SaveAuto(string content)
    {
        var path = GetAutoSavePath();
        File.WriteAllText(path, content);
    }

    public async Task<string?> LoadAuto()
    {
        var path = GetAutoSavePath();
        if (File.Exists(path))
            return File.ReadAllText(path);
        return null;
    }

    public async Task DeleteAuto()
    {
        var path = GetAutoSavePath();
        if (File.Exists(path))
            File.Delete(path);
    }
}
