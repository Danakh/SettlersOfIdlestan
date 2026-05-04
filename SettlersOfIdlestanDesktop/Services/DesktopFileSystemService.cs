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
    public async Task SaveText(string content)
    {
#if WINDOWS
        var picker = new FileSavePicker();
        var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Fichier JSON", new List<string> { ".json" });
        picker.SuggestedFileName = IFileSystemService.DefaultSaveName;
        StorageFile file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await FileIO.WriteTextAsync(file, content);
        }
#else
        // Fallback: save in app data directory
        var path = Path.Combine(FileSystem.Current.AppDataDirectory, IFileSystemService.DefaultSaveName);
        File.WriteAllText(path, content);
#endif
    }

    public async Task<string?> LoadText()
    {
        var options = new PickOptions
        {
            PickerTitle = "Charger une sauvegarde",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.MacCatalyst, new[] { ".json" } },
                // Linux n'est pas supporté par DevicePlatform
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
}
