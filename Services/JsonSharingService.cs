using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public class JsonSharingService
{
    private const string FolderName = "WeightSyncFinaleShared";
    private const string FileName = "current_weight.json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
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

    public async Task<string?> ReadRawAsync()
    {
        try
        {
            var p = FilePath;
            return File.Exists(p) ? await File.ReadAllTextAsync(p) : null;
        }
        catch { return null; }
    }

    public async Task<WeightData?> ReadWeightAsync()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path)) return null;

            var raw = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Normalise French decimal comma → dot
            raw = FixDecimalComma(raw);

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // ── Weight ────────────────────────────────────────────────────────
            double weight = 0;
            if (root.TryGetProperty("Weight", out var wEl))
            {
                if (wEl.ValueKind == JsonValueKind.Number)
                    weight = wEl.GetDouble();
                else if (wEl.ValueKind == JsonValueKind.String)
                    double.TryParse(wEl.GetString(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
            }

            // ── RawValue ──────────────────────────────────────────────────────
            string? rawValue = null;
            if (root.TryGetProperty("RawValue", out var rvEl))
                rawValue = rvEl.GetString();

            // ── UpdatedAt ─────────────────────────────────────────────────────
            DateTime updatedAt = DateTime.UtcNow;
            if (root.TryGetProperty("UpdatedAt", out var uEl) && uEl.GetString() is { } us)
                DateTime.TryParse(us, null, DateTimeStyles.RoundtripKind, out updatedAt);

            return new WeightData
            {
                Weight = weight,
                RawValue = rawValue,
                UpdatedAt = updatedAt,
                IsConnected = true   // file exists and was read → assume connected
            };
        }
        catch { return null; }
    }

    // ── Replace digit,digit → digit.digit only outside JSON strings ───────────
    private static string FixDecimalComma(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (escape) { sb.Append(c); escape = false; continue; }
            if (c == '\\') { sb.Append(c); escape = true; continue; }
            if (c == '"') { inString = !inString; sb.Append(c); continue; }

            if (!inString && c == ','
                && i > 0 && i < json.Length - 1
                && char.IsDigit(json[i - 1])
                && char.IsDigit(json[i + 1]))
                sb.Append('.');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}