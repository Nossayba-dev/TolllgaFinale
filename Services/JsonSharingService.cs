using System.Globalization;
using System.Text.Json;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

/// <summary>
/// Reads the shared JSON file written by App 1.
/// Security: file is optionally AES-256 encrypted — falls back to plain JSON.
/// Decimal normalisation handles French locale comma separators.
/// </summary>
public class JsonSharingService
{
    private const string FolderName = "WeightSyncFinaleShared";
    private const string FileName = "current_weight.json";

    private byte[]? _jsonKey;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public string FilePath
    {
        get
        {
#if WINDOWS
            return Path.Combine(
                System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.CommonApplicationData),
                FolderName, FileName);
#elif ANDROID
            return "/data/user/0/com.companyname.weightsyncfinale/files/"
                   + FolderName + "/" + FileName;
#else
            return Path.Combine(FileSystem.AppDataDirectory, FolderName, FileName);
#endif
        }
    }

    // ── Load encryption key once ──────────────────────────────────────────────
    private async Task EnsureKeyAsync()
    {
        _jsonKey ??= await SecurityService.GetOrCreateJsonKeyAsync();
    }

    // ── Read raw file content (for debug display) ────────────────────────────
    public async Task<string?> ReadRawAsync()
    {
        var path = FilePath;
        if (!File.Exists(path)) return null;
        try { return await File.ReadAllTextAsync(path); }
        catch { return null; }
    }

    // ── Main read method ──────────────────────────────────────────────────────
    public async Task<WeightData?> ReadWeightAsync()
    {
        var path = FilePath;
        if (!File.Exists(path)) return null;

        await EnsureKeyAsync();

        var raw = await File.ReadAllTextAsync(path);

        // Try AES-256 decryption first (if App 1 encrypts the file)
        string json;
        try
        {
            json = SecurityService.Decrypt(raw, _jsonKey!);
        }
        catch
        {
            // Not encrypted (plain JSON) — use as-is
            json = raw;
        }

        // Normalise French decimal comma → dot
        json = NormalizeDecimalSeparator(json);

        try
        {
            return JsonSerializer.Deserialize<WeightData>(json, _jsonOptions);
        }
        catch
        {
            return ParseManually(json);
        }
    }

    // ── Decimal normaliser ────────────────────────────────────────────────────
    private static string NormalizeDecimalSeparator(string json)
    {
        var sb = new System.Text.StringBuilder();
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char ch = json[i];
            if (ch == '"' && (i == 0 || json[i - 1] != '\\'))
                inString = !inString;

            if (!inString && ch == ',' && i > 0 && i < json.Length - 1
                && char.IsDigit(json[i - 1]) && char.IsDigit(json[i + 1]))
                sb.Append('.');
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    // ── Fallback manual parser ────────────────────────────────────────────────
    private static WeightData? ParseManually(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double weight = 0;
            if (root.TryGetProperty("Weight", out var wProp))
            {
                if (wProp.ValueKind == JsonValueKind.Number)
                    weight = wProp.GetDouble();
                else if (wProp.ValueKind == JsonValueKind.String)
                    double.TryParse(wProp.GetString(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
            }

            if ((weight == 0 || weight == 1)
                && root.TryGetProperty("RawValue", out var rvProp))
            {
                var raw = rvProp.GetString() ?? "";
                var numStr = new string(
                    raw.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray())
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
        catch { return null; }
    }
}