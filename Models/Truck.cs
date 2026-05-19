namespace TolllgaFinale.Models;

public class Truck
{
    public int      Id          { get; set; }
    public string   Matricule   { get; set; } = "";
    public double   Tare        { get; set; }
    public string?  DriverName  { get; set; }
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
}
