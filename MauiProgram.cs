using Microsoft.Extensions.Logging;
using TolllgaFinale.Services;

namespace TolllgaFinale;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",    "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf",   "OpenSansSemibold");
            });

        builder.Services.AddMauiBlazorWebView();

        // ── Register the JSON sharing service ──────────────────────────────
        builder.Services.AddSingleton<JsonSharingService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
