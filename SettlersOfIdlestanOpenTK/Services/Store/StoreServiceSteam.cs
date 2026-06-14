using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Localization;
using Steamworks;

namespace SettlersOfIdlestanOpenTK.Services.Store;

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
            return StoreConnectionStatus.NotDetected;
        }
        catch
        {
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

    public void Dispose()
    {
        if (IsAvailable) SteamAPI.Shutdown();
    }
}
