using System.Text;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Localization;
using Steamworks;

namespace SettlersOfIdlestanAvalonia.Desktop.Services.Store;

/// <summary>
/// Intégration Steam via Steamworks.NET.
/// ConnectionStatus = Connected si Steam est lancé et l'init réussit,
/// Failed si Steam est détecté mais refuse la connexion,
/// NotDetected si la DLL Steam est absente (Steam non installé).
/// </summary>
public class StoreServiceSteam : IStoreService
{
    public string Name => "Steam";
    public StoreConnectionStatus ConnectionStatus { get; }
    public bool IsAvailable => ConnectionStatus == StoreConnectionStatus.Connected;

    public StoreServiceSteam()
    {
        ConnectionStatus = TryInitialize();
    }

    private static StoreConnectionStatus TryInitialize()
    {
        try
        {
            if (!SteamAPI.Init()) return StoreConnectionStatus.Failed;
            // Demande asynchrone des stats — la réponse arrive via callback avant le premier prestige
            SteamUserStats.RequestCurrentStats();
            return StoreConnectionStatus.Connected;
        }
        catch (DllNotFoundException)
        {
            // Cas attendu et normal : le jeu tourne hors Steam (build itch.io, exécution locale).
            // Rien à signaler, NotDetected dit déjà tout.
            return StoreConnectionStatus.NotDetected;
        }
        catch (Exception ex)
        {
            // Ici en revanche steam_api est bien présente mais l'initialisation a échoué : succès et
            // sauvegarde cloud sont silencieusement désactivés pour toute la session. Sans trace, le
            // joueur constate seulement que ses succès ne se débloquent plus.
            GameLog.Error(nameof(StoreServiceSteam), nameof(TryInitialize), ex);
            return StoreConnectionStatus.Failed;
        }
    }

    public Language? GetPreferredLanguage()
    {
        if (!IsAvailable) return null;

        return SteamApps.GetCurrentGameLanguage() switch
        {
            "french"  => Language.French,
            "english" => Language.English,
            _         => Language.English,
        };
    }

    public void UnlockAchievement(string achievementId)
    {
        if (!IsAvailable) return;
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
    }

    /// <summary>
    /// Pousse le fichier vers Steam Cloud. Le client Steam gère seul le merge entre machines ;
    /// on écrit ici toujours la version locale, qui reste la source de vérité côté jeu.
    /// </summary>
    public void SaveCloudFile(string fileName, string content)
    {
        if (!IsAvailable) return;
        if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp()) return;

        var data = Encoding.UTF8.GetBytes(content);
        SteamRemoteStorage.FileWrite(fileName, data, data.Length);
    }

    public string? LoadCloudFile(string fileName)
    {
        if (!IsAvailable) return null;
        if (!SteamRemoteStorage.FileExists(fileName)) return null;

        int size = SteamRemoteStorage.GetFileSize(fileName);
        if (size <= 0) return null;

        var buffer = new byte[size];
        int read = SteamRemoteStorage.FileRead(fileName, buffer, size);
        if (read <= 0) return null;

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    public bool HasCloudFile(string fileName)
    {
        if (!IsAvailable) return false;
        return SteamRemoteStorage.FileExists(fileName);
    }

    public void Dispose()
    {
        if (IsAvailable) SteamAPI.Shutdown();
    }
}
