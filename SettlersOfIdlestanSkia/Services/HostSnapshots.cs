namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Instantanes d'etat destines aux controles d'overlay portes par l'hote (Avalonia).
///
/// Pourquoi des instantanes plutot que des accesseurs unitaires : chaque lecture traverse le
/// verrou qui serialise la boucle de jeu et le thread de rendu. Lire dix proprietes ferait dix
/// aller-retours, et surtout donnerait une vue incoherente si un Tick s'intercale au milieu.
/// Un instantane est construit en une seule prise de verrou.
///
/// Ce sont des records : la comparaison structurelle permet aux vues de ne se rafraichir que
/// lorsque l'etat a reellement change, plutot qu'a chaque tick de synchronisation.
/// </summary>
public sealed record TimeControlSnapshot(
    bool IsAvailable,
    bool IsPaused,
    int ActiveSpeed,
    long OfflineBankTicks)
{
    /// Etat neutre : pas de partie en cours (ecran titre, ou partie non encore chargee).
    public static readonly TimeControlSnapshot Unavailable = new(false, false, 1, 0);
}
