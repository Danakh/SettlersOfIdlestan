using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Trailer;

/// <summary>
/// IFileSystemService qui ne lit/écrit jamais rien : la génération de bande-annonce rejoue des saves
/// figées et ne doit ni les modifier ni dépendre d'un système de fichiers de jeu réel.
/// </summary>
internal sealed class NullFileSystemService : IFileSystemService
{
    public Task SaveText(string fileName, string content) => Task.CompletedTask;
    public Task<string?> LoadText(string fileName) => Task.FromResult<string?>(null);
    public Task SaveAuto(string content) => Task.CompletedTask;
    public Task<string?> LoadAuto() => Task.FromResult<string?>(null);
    public Task DeleteAuto() => Task.CompletedTask;
    public Task SaveSettings(string content) => Task.CompletedTask;
    public Task<string?> LoadSettings() => Task.FromResult<string?>(null);
    public Task SaveStats(string content) => Task.CompletedTask;
    public Task<string?> LoadStats() => Task.FromResult<string?>(null);
}
