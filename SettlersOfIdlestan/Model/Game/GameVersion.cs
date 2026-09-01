namespace SettlersOfIdlestan.Model.Game
{
    /// <summary>
    /// Numéro de version courant du jeu, écrit dans chaque sauvegarde (voir
    /// <see cref="MainGameState.SavedGameVersion"/> et <see cref="SettlersOfIdlestan.Controller.SaveController.Export"/>).
    /// Doit être tenu à jour en même temps que le changelog (voir CLAUDE.md) : les deux premiers
    /// chiffres (X.Y) correspondent à la dernière entrée "vX.Y" du changelog ; le troisième
    /// (patch) n'apparaît pas dans le changelog et doit être incrémenté manuellement pour un correctif.
    /// </summary>
    public static class GameVersion
    {
        public const string Current = "0.20.0";
    }
}
