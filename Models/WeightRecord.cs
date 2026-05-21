using SQLite;

namespace TolllgaFinale.Models;

public class WeightRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Matricule { get; set; } = "";
    public string? DriverName { get; set; }
    public double GrossWeight { get; set; }
    public double Tare { get; set; }
    public double NetWeight { get; set; }
    public DateTime WeighingDate { get; set; } = DateTime.UtcNow;
}