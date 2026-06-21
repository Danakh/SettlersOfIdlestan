namespace SettlersOfIdlestanSkia.Renderers.Debug;

public static class DebugSettings
{
    public static bool ShowHexCoords { get; set; } = false;
    public static bool ShowAutoplayerCommands { get; set; } = true;
    public static bool ShowFullMap { get; set; } = false;
    public static bool ExportTransparentBackground { get; set; } = false;

    /// True pendant l'export d'un screenshot avec interface : masque les éléments réservés au mode debug
    /// (ex. onglet Ascension) qui ne dépendent pas des toggles ci-dessus.
    public static bool SuppressDebugUiForExport { get; set; } = false;
}
