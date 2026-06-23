using System.Diagnostics;
using SOITrailerGenerator.Trailer;

namespace SOITrailerGenerator;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            bool skipFfmpeg = args.Contains("--skip-ffmpeg");
            string? explicitRoot = args.FirstOrDefault(a => !a.StartsWith("--"));
            string trailerRoot = explicitRoot ?? FindDefaultTrailerRoot();

            var service = new TrailerService();
            string ffmpegCommand = service.GenerateTrailer(trailerRoot, Console.WriteLine);

            Console.WriteLine();
            Console.WriteLine("Commande ffmpeg pour assembler la vidéo finale :");
            Console.WriteLine(ffmpegCommand);

            if (skipFfmpeg)
            {
                Console.WriteLine("(--skip-ffmpeg : encodage non lancé, exécutez la commande ci-dessus manuellement.)");
                return 0;
            }

            return RunFfmpeg(ffmpegCommand) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur : {ex.Message}");
            return 1;
        }
    }

    private static string FindDefaultTrailerRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SettlersOfIdlestan.slnx")))
                return Path.Combine(dir.FullName, "assets", "Trailer");
        }

        throw new InvalidOperationException(
            "Impossible de localiser SettlersOfIdlestan.slnx ; précisez le dossier assets/Trailer en argument.");
    }

    /// <summary>
    /// Lance directement la commande ffmpeg générée (sans passer par generate_trailer.bat), pour qu'une
    /// seule commande produise le .mp4 final. Cherche ffmpeg dans le PATH puis dans l'emplacement de
    /// fallback connu (cf. generate_trailer.bat) ; utiliser --skip-ffmpeg pour itérer sur les frames
    /// sans réencoder à chaque fois.
    /// </summary>
    private static bool RunFfmpeg(string ffmpegCommand)
    {
        string? ffmpegPath = FindFfmpegExecutable();
        if (ffmpegPath is null)
        {
            Console.Error.WriteLine(
                "ffmpeg introuvable dans le PATH ni dans l'emplacement connu. " +
                "Installez-le, ajoutez-le au PATH, puis exécutez la commande ci-dessus manuellement.");
            return false;
        }

        string arguments = ffmpegCommand[ffmpegCommand.IndexOf(' ')..].TrimStart();

        Console.WriteLine();
        Console.WriteLine("=== Assemblage de la vidéo avec ffmpeg ===");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false
        });

        process!.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"Échec de l'assemblage ffmpeg (code {process.ExitCode}).");
            return false;
        }

        Console.WriteLine("Vidéo générée avec succès.");
        return true;
    }

    private static string? FindFfmpegExecutable()
    {
        string exeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathEnv.Split(Path.PathSeparator))
        {
            string candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate)) return candidate;
        }

        const string fallbackDir = @"C:\Program Files\ffmpeg-8.1.1-essentials_build\bin";
        string fallbackCandidate = Path.Combine(fallbackDir, exeName);
        return File.Exists(fallbackCandidate) ? fallbackCandidate : null;
    }
}
