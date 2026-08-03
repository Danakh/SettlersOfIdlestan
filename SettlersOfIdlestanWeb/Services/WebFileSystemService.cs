using Microsoft.JSInterop;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanWeb.Services;

public class WebFileSystemService : IFileSystemService
{
    private readonly IJSRuntime _js;
    private const string AutoSaveKey    = "settlers_autosave";
    private const string SettingsKey    = "settlers_settings";
    private const string StatsKey       = "settlers_stats";

    public WebFileSystemService(IJSRuntime js) => _js = js;

    public async Task SaveText(string fileName, string content)
        => await _js.InvokeVoidAsync("gameInterop.downloadFile", fileName, content);

    public async Task<string?> LoadText(string fileName)
        => await _js.InvokeAsync<string?>("gameInterop.openFilePicker");

    public async Task SaveAuto(string content)
        => await _js.InvokeVoidAsync("localStorage.setItem", AutoSaveKey, content);

    public async Task<string?> LoadAuto()
        => await _js.InvokeAsync<string?>("localStorage.getItem", AutoSaveKey);

    public async Task DeleteAuto()
        => await _js.InvokeVoidAsync("localStorage.removeItem", AutoSaveKey);

    public async Task SaveSettings(string content)
        => await _js.InvokeVoidAsync("localStorage.setItem", SettingsKey, SaveController.Encrypt(content));

    public async Task<string?> LoadSettings()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", SettingsKey);
        return raw != null ? SaveController.DecodeToJson(raw) : null;
    }

    public async Task SaveStats(string content)
        => await _js.InvokeVoidAsync("localStorage.setItem", StatsKey, SaveController.Encrypt(content));

    public async Task<string?> LoadStats()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StatsKey);
        return raw != null ? SaveController.DecodeToJson(raw) : null;
    }
}
