

namespace TolllgaFinale.Models;

public class TicketData
{
    public int RecordId { get; set; }
    public string Matricule { get; set; } = "";
    public string? DriverName { get; set; }
    public string? Product { get; set; }
    public string? Observation { get; set; }
    public double GrossWeight { get; set; }
    public double Tare { get; set; }
    public double NetWeight => GrossWeight - Tare;
    public DateTime WeighingDateGross { get; set; }
    public DateTime WeighingDateTare { get; set; }
    public string? OperatorGross { get; set; }
    public string? OperatorTare { get; set; }

    public static TicketData FromRecord(WeightRecord r) => new()
    {
        RecordId = r.Id,
        Matricule = r.Matricule,
        DriverName = r.DriverName,
        Product = r.Product,
        Observation = r.Observation,
        GrossWeight = r.GrossWeight,
        Tare = r.Tare,
        WeighingDateGross = r.WeighingDateGross,
        WeighingDateTare = r.WeighingDateTare,
        OperatorGross = r.OperatorGross,
        OperatorTare = r.OperatorTare
    };
}