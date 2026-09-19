using SettlersOfIdlestanSkia.Services.Audio;

namespace SettlersOfIdlestanAvalonia.Browser.Services;

/// <summary>
/// Sortie audio du head navigateur, via Web Audio (voir la section « Bruitages » de
/// <c>wwwroot/soiInterop.js</c>). Pendant de <c>DesktopAudioService</c>, en beaucoup plus court :
/// le navigateur decode, reechantillonne et mixe lui-meme.
///
/// <para><b>Le son ne demarre qu'apres un geste du joueur.</b> Tous les navigateurs refusent
/// qu'une page emette avant un clic ou une touche ; l'AudioContext cree au chargement reste
/// « suspended » et tout declenchement est ignore. Le module JS le reveille au premier
/// pointerdown ou keydown. En pratique le joueur clique sur « Nouvelle partie » ou
/// « Continuer » avant le premier bruitage, la contrainte ne se voit donc pas.</para>
/// </summary>
public sealed class BrowserAudioService : IAudioService
{
    private bool _disposed;

    public void Load(SoundId id, byte[] wav)
    {
        if (_disposed) return;
        BrowserInterop.AudioLoad(id.ToString(), wav);
    }

    public void Play(SoundId id, float volume)
    {
        if (_disposed) return;
        BrowserInterop.AudioPlay(id.ToString(), volume);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        BrowserInterop.AudioClose();
    }
}
