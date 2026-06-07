using Microsoft.Extensions.Logging;
using SettlersOfIdlestanDesktop.Services;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Globalization;

namespace SettlersOfIdlestanDesktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}
}
