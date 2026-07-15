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
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<JsonSharingService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<PrinterSettingsService>();

#if WINDOWS
        builder.Services.AddSingleton<IPrintService, WindowsPrintService>();
#endif
#if ANDROID
        builder.Services.AddSingleton<IPrintService, AndroidStandardPrintService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}