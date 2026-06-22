using System.Text.Json.Serialization;

namespace TolllgaFinale.Models;

public class WeightData
{
    public double Weight { get; set; }
    public string? RawValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? StopTime { get; set; }

    // Default TRUE — if App 1 doesn't write this field, we assume it's connected
    [JsonPropertyName("IsConnected")]
    public bool IsConnected { get; set; } = true;
}