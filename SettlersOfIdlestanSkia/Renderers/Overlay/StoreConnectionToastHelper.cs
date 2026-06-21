using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Affiche un toast pour chaque store détecté (connecté ou en échec de connexion).
/// </summary>
internal static class StoreConnectionToastHelper
{
    public static void ShowConnectionToasts(StoreController? storeController, NotificationToastRenderer? toastRenderer, LocalizationService localization)
    {
        if (storeController == null || toastRenderer == null) return;

        foreach (var (name, status) in storeController.GetConnectionStatuses())
        {
            if (status == StoreConnectionStatus.Connected)
            {
                string msg = localization.GetFormated("notification_store_connected", name);
                toastRenderer.ShowNotification(msg, string.Empty, NotificationIcon.StoreOk);
            }
            else if (status == StoreConnectionStatus.Failed)
            {
                string msg = localization.GetFormated("notification_store_failed", name);
                toastRenderer.ShowNotification(msg, string.Empty, NotificationIcon.StoreFail);
            }
        }
    }
}
