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
        /// Load the game from the auto-save file. Returns null if not found.
        /// </summary>
        Task<string?> LoadAuto();
    }
}
