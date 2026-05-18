using Android.Content;
using Android.Provider;
using AndroidBuild = Android.OS.Build;
using AndroidBuildVersionCodes = Android.OS.BuildVersionCodes;
using AndroidEnvironment = Android.OS.Environment;

namespace TolllgaFinale.Platforms.AndroidHelpers;

public static class StoragePermissionHelper
{
    public static bool HasPermission()
    {
        if (AndroidBuild.VERSION.SdkInt >= AndroidBuildVersionCodes.R)
            return AndroidEnvironment.IsExternalStorageManager;

        return true;
    }

    public static void RequestPermission()
    {
        if (AndroidBuild.VERSION.SdkInt >= AndroidBuildVersionCodes.R)
        {
            var intent = new Intent(Settings.ActionManageAllFilesAccessPermission);
            intent.AddFlags(ActivityFlags.NewTask);
            global::Android.App.Application.Context.StartActivity(intent);
        }
    }
}