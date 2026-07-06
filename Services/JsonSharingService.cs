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
    // FIX: The old char-by-char method failed on cases like {"Weight": 72,5, "RawValue":...}
    // because it checked json[i+1] for a digit, but the character after "5" in "72,5,"
    // is a comma (JSON field separator), not a digit — so the replacement never fired.
    // The regex (\d),(\d) correctly identifies decimal commas: a comma sitting between
    // two digit characters is always a decimal separator, never a JSON structural comma.
    private static string NormalizeDecimalSeparator(string json)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(\d),(\d)",
            "$1.$2");
    }

    // ── Fallback manual parser ────────────────────────────────────────────────
    // FIX: The old code had `if ((weight == 0 || weight == 1) && ...)` which discarded
    // a legitimate weight of exactly 1.000 kg and tried to re-parse from RawValue,
    // which would often fail and return 0. Now we use a `weightParsed` flag and only
    // fall back to RawValue when the Weight field genuinely could not be parsed at all.
    private static WeightData? ParseManually(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double weight = 0;
            bool weightParsed = false;

            if (root.TryGetProperty("Weight", out var wProp))
            {
                if (wProp.ValueKind == JsonValueKind.Number)
                {
                    weight = wProp.GetDouble();
                    weightParsed = true;
                }
                else if (wProp.ValueKind == JsonValueKind.String)
                {
                    weightParsed = double.TryParse(wProp.GetString(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
                }
            }

            // Only fall back to RawValue if Weight could NOT be parsed at all
            if (!weightParsed && root.TryGetProperty("RawValue", out var rvProp))
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