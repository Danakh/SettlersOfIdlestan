using SOITrailerGenerator.Trailer;

namespace SOITrailerGenerator;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string trailerRoot = args.Length > 0 ? args[0] : FindDefaultTrailerRoot();
            var service = new TrailerService();
            string ffmpegCommand = service.GenerateTrailer(trailerRoot, Console.WriteLine);

            Console.WriteLine();
            Console.WriteLine("Commande ffmpeg pour assembler la vidéo finale :");
            Console.WriteLine(ffmpegCommand);
            return 0;
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
}
