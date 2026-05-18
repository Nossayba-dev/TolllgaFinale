using System.Text.Json;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public class JsonSharingService
{
    private const string FolderName = "WeightSyncFinaleShared";
    private const string FileName = "current_weight.json";

    public string FilePath
    {
        get
        {
#if WINDOWS
            return Path.Combine(
                System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.CommonApplicationData),
                FolderName,
                FileName);
#elif ANDROID
            // App 1 (com.companyname.weightsyncfinale) writes to its private files dir.
            // We build the path directly using the known package name.
            return "/data/user/0/com.companyname.weightsyncfinale/files/"
                   + FolderName + "/" + FileName;
#else
            return Path.Combine(FileSystem.AppDataDirectory, FolderName, FileName);
#endif
        }
    }

    public async Task<WeightData?> ReadWeightAsync()
    {
        var path = FilePath;
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);

        return JsonSerializer.Deserialize<WeightData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}