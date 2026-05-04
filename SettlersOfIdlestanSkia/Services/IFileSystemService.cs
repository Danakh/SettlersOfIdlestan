namespace SettlersOfIdlestanSkia.Services
{
    /// <summary>
    /// Abstraction for file system operations (save/load game state).
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// Save text content to a file.
        /// </summary>
        void SaveText(string fileName, string content);

        /// <summary>
        /// Load text content from a file. Returns null if file does not exist or error.
        /// </summary>
        string? LoadText(string fileName);
    }
}
