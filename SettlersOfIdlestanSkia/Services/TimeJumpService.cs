using System;
using System.Diagnostics;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Saut de temps en cours. Consomme la banque hors-ligne par tranches bornees en temps, au lieu
/// de simuler tout le saut dans l'appel qui le declenche.
///
/// Un saut d'une heure de jeu represente 360 000 ticks : simule d'un bloc, il fige la fenetre
/// plusieurs secondes sans que rien n'indique au joueur que le jeu travaille — et sur le head
/// navigateur, l'onglet passe pour bloque. Decoupe ici, le tick rend la main a l'hote entre deux
/// tranches, qui affiche une barre de progression.
/// </summary>
public sealed class TimeJumpService
{
    /// <summary>
    /// Taille d'une tranche simulee. Meme valeur que le decoupage historique du saut de merveille
    /// (<c>GameClock.AdvanceFromBank(needed, 10000)</c>) : chaque comportement periodique n'a
    /// qu'une chance d'agir par evenement <c>Advanced</c>, changer cette taille changerait ce que
    /// le saut produit.
    /// </summary>
    private const long ChunkTicks = 10_000;

    /// <summary>
    /// Temps maximal passe a simuler dans un tick de la boucle de jeu. Au-dela, on rend la main
    /// pour que l'hote redessine la barre de progression et reste reactif.
    /// </summary>
    private const double FrameBudgetMs = 30;

    private readonly Stopwatch _budget = new();

    private long _totalTicks;
    private long _doneTicks;

    public bool IsActive { get; private set; }

    /// <summary>Cle de localisation du motif du saut, affichee sous la barre de progression.</summary>
    public string ReasonKey { get; private set; } = "";

    public long TotalTicks => _totalTicks;
    public long DoneTicks => _doneTicks;

    public double Progress => _totalTicks > 0 ? Math.Clamp((double)_doneTicks / _totalTicks, 0d, 1d) : 0d;

    /// <summary>
    /// Programme un saut de <paramref name="ticks"/> ticks preleves sur la banque hors-ligne.
    /// Sans effet si un saut est deja en cours ou si la banque est insuffisante.
    /// </summary>
    public bool Request(GameClock clock, long ticks, string reasonKey)
    {
        if (IsActive || ticks <= 0 || clock.OfflineBankTicks < ticks) return false;

        IsActive    = true;
        _totalTicks = ticks;
        _doneTicks  = 0;
        ReasonKey   = reasonKey;
        return true;
    }

    /// <summary>
    /// Simule la prochaine tranche du saut. A appeler une fois par tick de la boucle de jeu tant
    /// que <see cref="IsActive"/> est vrai, a la place de l'avancement en temps reel.
    /// </summary>
    public void Advance(GameClock clock)
    {
        if (!IsActive) return;

        _budget.Restart();
        do
        {
            long chunk = Math.Min(ChunkTicks, _totalTicks - _doneTicks);

            // La banque est prelevee tranche par tranche et non d'un bloc au depart : si la partie
            // se ferme au milieu du saut, le temps pas encore simule reste acquis au joueur.
            if (chunk <= 0 || !clock.AdvanceFromBank(chunk, chunk)) { Finish(); return; }

            _doneTicks += chunk;
        }
        while (_doneTicks < _totalTicks && _budget.Elapsed.TotalMilliseconds < FrameBudgetMs);

        if (_doneTicks >= _totalTicks) Finish();
    }

    /// <summary>Interrompt le saut en cours ; les ticks pas encore consommes restent en banque.</summary>
    public void Cancel() => Finish();

    private void Finish()
    {
        IsActive    = false;
        _totalTicks = 0;
        _doneTicks  = 0;
        ReasonKey   = "";
        _budget.Reset();
    }
}
