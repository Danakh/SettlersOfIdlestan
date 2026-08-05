using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public enum NotificationIcon { Info, Achievement, StoreOk, StoreFail }

/// <summary>
/// Machine à états des toasts de notification : les crée, les fait vieillir et les expose en
/// instantané. Ne dessine plus rien — la pile est une vue Avalonia.
/// </summary>
public sealed class NotificationToastRenderer : IDisposable
{
    private sealed record ActiveToast(
        long Id,
        string Title,
        string Message,
        NotificationIcon Icon,
        float TotalDuration)
    {
        public float TimeLeft { get; set; } = TotalDuration;
    }

    private long _nextToastId;

    // Sert au seul instantané : c'est lui qui dit à la vue de quel côté ancrer la pile.
    private readonly UILayoutService _layout;
    private readonly List<ActiveToast> _toasts = [];

    private const int   MaxToasts      = 3;
    private const float ToastDuration  = 5f;
    private const float FadeOutTime    = 0.4f;
    private const float SlideInTime    = 0.25f;

    private bool _disposed;

    public NotificationToastRenderer(UILayoutService layout) => _layout = layout;

    public void ShowNotification(string title, string message, NotificationIcon icon = NotificationIcon.Info)
    {
        // Dépile le plus ancien si on est au max
        if (_toasts.Count >= MaxToasts)
            _toasts.RemoveAt(0);

        _toasts.Add(new ActiveToast(_nextToastId++, title, message, icon, ToastDuration));
    }

    /// <summary>Instantané pour la vue Avalonia : contenu, ordre d'empilement et opacité.</summary>
    public ToastListSnapshot GetSnapshot()
    {
        if (_disposed || _toasts.Count == 0) return ToastListSnapshot.Empty;

        var toasts = new List<ToastSnapshot>(_toasts.Count);

        // Ordre inverse de la liste : le rendu Skia empile le plus ancien en bas et les plus
        // récents au-dessus. Une pile Avalonia ancrée en bas se remplit de haut en bas, il faut
        // donc lui donner le plus récent en premier pour retrouver le même ordre à l'écran.
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            var toast = _toasts[i];
            toasts.Add(new ToastSnapshot(toast.Id, toast.Title, toast.Message, toast.Icon, ComputeOpacity(toast)));
        }

        return new ToastListSnapshot(toasts, _layout.TabsAtBottom);
    }

    private static double ComputeOpacity(ActiveToast toast)
    {
        float elapsed = toast.TotalDuration - toast.TimeLeft;
        if (elapsed < SlideInTime) return elapsed / SlideInTime;
        if (toast.TimeLeft < FadeOutTime) return toast.TimeLeft / FadeOutTime;
        return 1d;
    }

    /// <summary>Ferme un toast depuis une vue portée par l'hôte.</summary>
    public void Dismiss(long id)
    {
        int index = _toasts.FindIndex(t => t.Id == id);
        if (index >= 0) _toasts.RemoveAt(index);
    }

    /// <summary>
    /// Fait vieillir les toasts et retire ceux qui ont expiré.
    ///
    /// C'est la boucle de jeu qui appelle cette méthode. Loger le décompte dans un rendu, comme
    /// le faisait la version Skia, ne marcherait plus : ce renderer ne dessine plus rien, et les
    /// toasts resteraient affichés indéfiniment. Il n'est plus que la machine à états.
    /// </summary>
    public void Advance(float deltaTime)
    {
        if (_disposed) return;

        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            _toasts[i].TimeLeft -= deltaTime;
            if (_toasts[i].TimeLeft <= 0f)
                _toasts.RemoveAt(i);
        }
    }


    public void Dispose() => _disposed = true;
}
