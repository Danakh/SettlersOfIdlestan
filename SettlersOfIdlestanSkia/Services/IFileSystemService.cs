namespace SettlersOfIdlestanSkia.Services
{
    /// <summary>
    /// Abstraction for file system operations (save/load game state).
    /// </summary>
    public interface IFileSystemService
    {
        static protected string DefaultSaveName = "idlestan.save";

        /// <summary>
        /// Save text content to a file.
        /// </summary>
        Task SaveText(string fileName, string content);

        /// <summary>
        /// Load text content from a file. Returns null if file does not exist or error.
        /// </summary>
        Task<string?> LoadText(string fileName);

        /// <summary>
        /// Save the game to the auto-save file.
        /// </summary>
        Task SaveAuto(string content);

        /// <summary>
        /// Variante octets de <see cref="SaveAuto(string)"/>, alimentée par
        /// <c>MainGameController.ExportMainStateUtf8</c> : le contenu est déjà l'ASCII du Base64,
        /// une implémentation qui écrit un fichier peut le poser tel quel sans le transcoder.
        /// L'implémentation par défaut retombe sur la variante texte, pour les backends dont le
        /// support est de toute façon une chaîne (localStorage du head navigateur).
        /// </summary>
        Task SaveAuto(ReadOnlyMemory<byte> utf8Content)
            => SaveAuto(System.Text.Encoding.UTF8.GetString(utf8Content.Span));

        /// <summary>
        /// Load the game from the auto-save file. Returns null if not found.
        /// </summary>
        Task<string?> LoadAuto();

        /// <summary>
        /// Delete the auto-save file.
        /// </summary>
        Task DeleteAuto();

        /// <summary>
        /// Save the game settings to a dedicated settings file.
        /// </summary>
        Task SaveSettings(string content);

        /// <summary>
        /// Load the game settings from the dedicated settings file. Returns null if not found.
        /// </summary>
        Task<string?> LoadSettings();

        /// <summary>
        /// Save the player's lifetime statistics to a dedicated file, independent of the save
        /// game and of the settings file. Must survive "New Game" / hard reset.
        /// </summary>
        Task SaveStats(string content);

        /// <summary>
        /// Load the player's lifetime statistics from the dedicated stats file. Returns null if not found.
        /// </summary>
        Task<string?> LoadStats();
    }
}
