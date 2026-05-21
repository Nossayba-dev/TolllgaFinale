using System.Globalization;
using System.Text.Json;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public class JsonSharingService
{
    private const string FolderName = "WeightSyncFinaleShared";
    private const string FileName = "current_weight.json";

    // ── Shared JSON options — always invariant culture ────────────────────────
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

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

        // Normalize: replace comma-decimal numbers in JSON
        // e.g. "Weight": 0,86  →  "Weight": 0.86
        json = NormalizeDecimalSeparator(json);

        try
        {
            return JsonSerializer.Deserialize<WeightData>(json, _jsonOptions);
        }
        catch
        {
            // Fallback: manual parse of Weight from RawValue
            return ParseManually(json);
        }
    }

    // ── Replace comma decimals inside JSON numeric values ─────────────────────
    // Matches patterns like: "Weight": 0,86  or  "Weight":0,86
    private static string NormalizeDecimalSeparator(string json)
    {
        // Replace only digit,digit patterns (not commas in strings)
        var sb = new System.Text.StringBuilder();
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char ch = json[i];
            if (ch == '"' && (i == 0 || json[i - 1] != '\\'))
                inString = !inString;

            // Replace comma between digits only when outside a string
            if (!inString && ch == ',' && i > 0 && i < json.Length - 1
                && char.IsDigit(json[i - 1]) && char.IsDigit(json[i + 1]))
            {
                sb.Append('.');
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    // ── Last-resort manual parser using RawValue ──────────────────────────────
    private static WeightData? ParseManually(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double weight = 0;

            // Try Weight field first
            if (root.TryGetProperty("Weight", out var wProp))
            {
                if (wProp.ValueKind == JsonValueKind.Number)
                    weight = wProp.GetDouble();
                else if (wProp.ValueKind == JsonValueKind.String)
                    double.TryParse(wProp.GetString(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
            }

            // If Weight is 0 or 1 (truncated), try to extract from RawValue
            if ((weight == 0 || weight == 1)
                && root.TryGetProperty("RawValue", out var rvProp))
            {
                var raw = rvProp.GetString() ?? "";
                // Extract numeric part: keep digits, dot, comma
                var numStr = new string(raw
                    .Where(c => char.IsDigit(c) || c == '.' || c == ',')
                    .ToArray())
                    .Replace(',', '.');
                double.TryParse(numStr,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
            }

            DateTime updatedAt = DateTime.UtcNow;
            if (root.TryGetProperty("UpdatedAt", out var uProp))
                DateTime.TryParse(uProp.GetString(), out updatedAt);

            bool isConnected = true;
            if (root.TryGetProperty("IsConnected", out var cProp))
                isConnected = cProp.GetBoolean();

            DateTime? startTime = null, stopTime = null;
            if (root.TryGetProperty("StartTime", out var stProp)
                && DateTime.TryParse(stProp.GetString(), out var st))
                startTime = st;
            if (root.TryGetProperty("StopTime", out var spProp)
                && DateTime.TryParse(spProp.GetString(), out var sp))
                stopTime = sp;

            return new WeightData
            {
                Weight = weight,
                RawValue = root.TryGetProperty("RawValue", out var rv)
                              ? rv.GetString() : null,
                UpdatedAt = updatedAt,
                IsConnected = isConnected,
                StartTime = startTime,
                StopTime = stopTime
            };
        }
        catch
        {
            return null;
        }
    }
}