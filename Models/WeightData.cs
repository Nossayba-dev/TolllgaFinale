namespace TolllgaFinale.Models;

public class WeightData
{
    public double Weight { get; set; }
    public string? RawValue { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartTime { get; set; }  
    public DateTime? StopTime { get; set; }
    public bool IsConnected { get; set; }
}