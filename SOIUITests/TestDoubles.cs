using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.Controls;
using SkiaSharp;

namespace SOIUITests;

/// <summary>Carte factice : compte les clics recus et dessine une couleur reconnaissable.</summary>
public sealed class ProbeMapControl : SkiaCanvasControl
{
    public static readonly SKColor FillColor = new(18, 24, 38);

    public int PointerPressedCount { get; private set; }

    protected override void OnRenderSkia(SKCanvas canvas, SKSize size)
    {
        canvas.Clear(FillColor);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPressedCount++;
        e.Handled = true;
    }
}

/// <summary>Systeme de fichiers factice : retient ce qui a ete ecrit ou supprime.</summary>
public sealed class FakeFileSystemService : SettlersOfIdlestanSkia.Services.IFileSystemService
{
    public List<string> SavedFiles { get; } = [];
    public bool AutoDeleted { get; private set; }

    public Task SaveText(string fileName, string content)
    {
        SavedFiles.Add(fileName);
        return Task.CompletedTask;
    }

    public Task DeleteAuto()
    {
        AutoDeleted = true;
        return Task.CompletedTask;
    }

    public Task<string?> LoadText(string fileName) => Task.FromResult<string?>(null);
    public Task SaveAuto(string content) => Task.CompletedTask;
    public Task<string?> LoadAuto() => Task.FromResult<string?>(null);
    public Task SaveSettings(string content) => Task.CompletedTask;
    public Task<string?> LoadSettings() => Task.FromResult<string?>(null);
    public Task SaveStats(string content) => Task.CompletedTask;
    public Task<string?> LoadStats() => Task.FromResult<string?>(null);
}

/// <summary>
/// Reproduit la superposition reelle du jeu : la carte occupe tout l'espace,
/// un panneau d'overlay opaque a l'input est pose par-dessus.
/// </summary>
public sealed class ProbeGameWindow : Window
{
    public ProbeMapControl Map { get; }
    public Button OverlayButton { get; }
    public int OverlayButtonClicks { get; private set; }

    public ProbeGameWindow()
    {
        Width = 800;
        Height = 600;

        Map = new ProbeMapControl();

        OverlayButton = new Button { Content = "Construire", Width = 160, Height = 60 };
        OverlayButton.Click += (_, _) => OverlayButtonClicks++;

        var overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 40)),
            Width = 200,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = OverlayButton,
        };

        Content = new Grid { Children = { Map, overlay } };
    }
}
