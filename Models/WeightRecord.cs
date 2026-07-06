using SQLite;

namespace TolllgaFinale.Models;

public class WeightRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Matricule { get; set; } = "";
    public string? DriverName { get; set; }
    public string? Product { get; set; }
    public string? Observation { get; set; }
    public double Amount { get; set; }
    public double GrossWeight { get; set; }
    public double Tare { get; set; }
    public double NetWeight { get; set; }
    public DateTime WeighingDateTare { get; set; }
    public DateTime WeighingDateGross { get; set; }
    public string? OperatorTare { get; set; }
    public string? OperatorGross { get; set; }
}