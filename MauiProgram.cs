using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using TolllgaFinale.Services;

namespace TolllgaFinale;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;

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
        builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
        builder.Services.AddSingleton<PrinterSettingsService>();

#if WINDOWS
        builder.Services.AddSingleton<IPrinterCatalogService, WindowsPrinterCatalogService>();
        builder.Services.AddSingleton<WindowsUsbPrinterTransport>();
        builder.Services.AddSingleton<IPrinterService, WindowsUsbPrinterService>();
#elif ANDROID
        builder.Services.AddSingleton<IPrinterCatalogService, UnsupportedPrinterCatalogService>();
        builder.Services.AddSingleton<IUsbPermissionService, AndroidUsbPermissionService>();
        builder.Services.AddSingleton<AndroidUsbPrinterTransport>();
        builder.Services.AddSingleton<IPrinterService, AndroidUsbPrinterService>();
#else
        builder.Services.AddSingleton<IPrinterCatalogService, UnsupportedPrinterCatalogService>();
        builder.Services.AddSingleton<IPrinterService, UnsupportedPrinterService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}