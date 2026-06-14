using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestan.Model.Tasks;
using Steamworks;

namespace SettlersOfIdlestanOpenTK.Services.Store;

/// <summary>
/// Intégration Steam via Steamworks.NET.
/// IsAvailable retourne false si Steam n'est pas lancé ou si steam_appid.txt est absent.
/// </summary>
public class StoreServiceSteam : IStoreService
{
    private readonly bool _initialized;

    public StoreServiceSteam()
    {
        _initialized = TryInitialize();
    }

    public bool IsAvailable => _initialized;

    private static bool TryInitialize()
    {
        try
        {
            if (!SteamAPI.Init()) return false;
            // Demande asynchrone des stats — la réponse arrive via callback avant le premier prestige
            SteamUserStats.RequestCurrentStats();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Language? GetPreferredLanguage()
    {
        if (!_initialized) return null;

        return SteamApps.GetCurrentGameLanguage() switch
        {
            "french"  => Language.French,
            "english" => Language.English,
            _         => Language.English,
        };
    }

    public void SendStats(GameRecord gameRecord)
    {
        if (!_initialized) return;

        SteamUserStats.SetStat("max_prestige_points_single_run", gameRecord.MaxPrestigePointsInSingleRun);
        SteamUserStats.StoreStats();
    }

    public void UnlockAchievement(string achievementId)
    {
        if (!_initialized) return;
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
    }

    public void Dispose()
    {
        if (_initialized) SteamAPI.Shutdown();
    }
}
