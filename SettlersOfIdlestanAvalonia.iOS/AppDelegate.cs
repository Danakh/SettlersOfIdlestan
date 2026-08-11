using Avalonia;
using Avalonia.iOS;
using Foundation;
using SettlersOfIdlestanUI;

namespace SettlersOfIdlestanAvalonia.iOS;

/// <summary>
/// Pendant iOS de <c>Program</c> cote Desktop : c'est cet objet qui construit l'AppBuilder.
/// <see cref="AvaloniaAppDelegate{TApp}"/> se charge de creer la fenetre, la vue racine et le
/// cycle de vie mono-vue ; il n'y a donc rien a cabler ici en dehors des polices et du log.
/// </summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithGameFonts().LogToTrace();
}
