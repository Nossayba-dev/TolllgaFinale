using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

public interface IPrintService
{
    /// <summary>Imprime le ticket. printerName = null → imprimante par défaut.</summary>
    Task<bool> PrintTicketAsync(TicketData ticket, string? printerName = null);

    /// <summary>Liste des imprimantes installées (pour un menu déroulant côté UI).</summary>
    List<string> GetInstalledPrinters();
}