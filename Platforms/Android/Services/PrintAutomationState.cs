namespace TolllgaFinale.Services;

/// <summary>
/// État partagé en mémoire entre AndroidStandardPrintService (qui lance l'impression) et
/// PrintAutomationAccessibilityService (qui automatise l'écran système résultant). Les deux
/// s'exécutent dans le même processus applicatif, donc un simple champ statique suffit — Android
/// ne fournit aucune API permettant de transmettre le nombre de copies directement à PrintManager.
/// </summary>
internal static class PrintAutomationState
{
    public static int PendingCopies { get; set; } = 1;
}
