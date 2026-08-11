using UIKit;

namespace SettlersOfIdlestanAvalonia.iOS;

internal static class Program
{
    // Point d'entree natif : UIKit instancie l'AppDelegate, qui demarre Avalonia.
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
