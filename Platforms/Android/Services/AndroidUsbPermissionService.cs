using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;

namespace TolllgaFinale.Services;

/// <summary>
/// Android USB permission helper that requests access to a device before printing.
/// The printer transport relies on this service to keep permission logic separate.
/// </summary>
public sealed class AndroidUsbPermissionService : IUsbPermissionService
{
    private const string ActionUsbPermission = "TolllgaFinale.USB_PERMISSION";

    public async Task<bool> EnsurePermissionAsync(int vendorId, int productId, CancellationToken cancellationToken = default)
    {
        var manager = GetUsbManager();
        if (manager is null)
            return false;

        var device = FindDevice(manager, vendorId, productId);
        if (device is null)
            return false;

        if (manager.HasPermission(device))
            return true;

        var context = Android.App.Application.Context;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var receiver = new UsbPermissionReceiver(tcs);

        var filter = new IntentFilter(ActionUsbPermission);
        context.RegisterReceiver(receiver, filter);

        try
        {
            var intent = new Intent(ActionUsbPermission);
            var flags = PendingIntentFlags.UpdateCurrent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                flags |= PendingIntentFlags.Mutable;

            var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, flags);
            manager.RequestPermission(device, pendingIntent);

            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
        finally
        {
            try { context.UnregisterReceiver(receiver); } catch { }
        }
    }

    private static UsbManager? GetUsbManager()
        => Android.App.Application.Context.GetSystemService(Context.UsbService) as UsbManager;

    private static UsbDevice? FindDevice(UsbManager manager, int vendorId, int productId)
    {
        foreach (var device in manager.DeviceList.Values)
        {
            if (device.VendorId == vendorId && device.ProductId == productId)
                return device;
        }

        return null;
    }

    private sealed class UsbPermissionReceiver : BroadcastReceiver, IDisposable
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public UsbPermissionReceiver(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action != ActionUsbPermission)
                return;

            var granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            _tcs.TrySetResult(granted);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
