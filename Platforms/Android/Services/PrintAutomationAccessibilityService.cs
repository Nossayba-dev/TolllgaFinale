using System.Linq;
using System.Threading;
using Android.AccessibilityServices;
using Android.App;
using Android.OS;
using Android.Views.Accessibility;

namespace TolllgaFinale.Services;

/// <summary>
/// Service d'accessibilité qui surveille l'apparition de l'écran système d'impression
/// (com.android.printspooler) et clique automatiquement sur son bouton "Imprimer", pour que
/// l'utilisateur n'ait pas à confirmer manuellement à chaque impression.
///
/// Doit être activé une seule fois par l'utilisateur dans Paramètres Android > Accessibilité >
/// TolllgaFinale (aucune API ne permet à une application d'activer ce type de service elle-même).
/// </summary>
[Service(Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/accessibility_service_config")]
public class PrintAutomationAccessibilityService : AccessibilityService
{
    private const string LogTag = "TolllgaFinalePrintAuto";
    private const string PrintSpoolerPackage = "com.android.printspooler";

    // Empêche plusieurs tentatives d'automatisation de tourner en même temps si plusieurs
    // événements WindowStateChanged arrivent coup sur coup pour le même écran (observé dans les
    // logs : plusieurs clics automatiques déclenchés en moins d'une seconde).
    private int _isHandling;

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (e?.EventType != EventTypes.WindowStateChanged)
            return;

        if (e.PackageName?.ToString() != PrintSpoolerPackage)
            return;

        if (Interlocked.CompareExchange(ref _isHandling, 1, 0) != 0)
        {
            Android.Util.Log.Debug(LogTag, "Automatisation déjà en cours pour cet écran, événement ignoré.");
            return;
        }

        Android.Util.Log.Debug(LogTag, "Écran d'impression système détecté, recherche du bouton Imprimer...");
        _ = TryClickPrintButtonAsync();
    }

    public override void OnInterrupt()
    {
    }

    private async Task TryClickPrintButtonAsync()
    {
        try
        {
            await RunAsync();
        }
        finally
        {
            // Libère le verrou une fois cette tentative terminée (succès, échec ou exception), pour
            // que le prochain écran d'impression puisse être traité normalement.
            Interlocked.Exchange(ref _isHandling, 0);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            // L'arborescence de l'écran peut ne pas être immédiatement disponible au moment de l'événement.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var root = RootInActiveWindow;
                if (root is not null)
                {
                    var printButton = FindClickableByText(root, "print", "imprimer");
                    if (printButton is not null)
                    {
                        // D'après le code source AOSP de PrintActivity (packages/PrintSpooler), le champ
                        // copies_edittext existe toujours dans l'écran mais n'est visible qu'après avoir
                        // cliqué more_options_button (un bouton distinct de "Expand handle", qu'on avait
                        // pris à tort pour le bon élément lors des tentatives précédentes).
                        try
                        {
                            await SetCopiesAsync(root, PrintAutomationState.PendingCopies);
                        }
                        catch (Exception ex)
                        {
                            Android.Util.Log.Error(LogTag, $"Exception pendant le réglage des copies (sans conséquence, on continue vers le clic) : {ex}");
                        }

                        // Le clic Imprimer se fait sur l'écran principal : on le rafraîchit au cas où le
                        // retour arrière ci-dessus aurait changé l'arborescence.
                        root = RootInActiveWindow ?? root;
                        printButton = FindClickableByText(root, "print", "imprimer") ?? printButton;

                        var clicked = printButton.PerformAction(Android.Views.Accessibility.Action.Click);
                        Android.Util.Log.Debug(LogTag, clicked
                            ? "Bouton Imprimer cliqué automatiquement."
                            : "Bouton Imprimer trouvé mais le clic automatique a échoué.");
                        return;
                    }
                }

                await Task.Delay(150);
            }

            Android.Util.Log.Error(LogTag, "Bouton Imprimer introuvable sur l'écran système d'impression après plusieurs tentatives.");
        }
        catch (Exception ex)
        {
            // Filet de sécurité global : ne jamais laisser une exception disparaître silencieusement
            // dans cette tâche "fire and forget" sans au moins la journaliser.
            Android.Util.Log.Error(LogTag, $"Exception inattendue dans l'automatisation d'impression : {ex}");
        }
    }

    /// <summary>
    /// Règle le nombre de copies. L'écran de cet appareil utilise "android.support.v7" (bibliothèque
    /// de support historique, pas AndroidX) : c'est une version d'Android ancienne (~5-7), dont
    /// l'implémentation de PrintActivity diffère du code source "master" actuel d'AOSP — les id
    /// "more_options_button"/"copies_edittext" qu'on y a trouvés ne s'appliquent donc pas forcément
    /// ici (déjà confirmé absents sur cet écran). On combine trois pistes, de la plus fiable à la plus
    /// empirique : id de vue (au cas où), puis clic sur la ligne résumé "Copies" (seul clic ayant
    /// concrètement changé l'écran lors des tests), puis clic sur "Expand handle" en dernier recours.
    /// Ne s'exécute que si copies > 1 est réellement demandé.
    /// </summary>
    private async Task SetCopiesAsync(AccessibilityNodeInfo mainScreenRoot, int copies)
    {
        if (copies <= 1)
            return;

        var candidates = new (string Label, Func<AccessibilityNodeInfo, AccessibilityNodeInfo?> Find)[]
        {
            ("id more_options_button", root => root.FindAccessibilityNodeInfosByViewId("com.android.printspooler:id/more_options_button")?.FirstOrDefault()),
            ("texte 'more options'", root => FindNodeByDescriptionContains(root, "more options")),
            ("ligne résumé Copies", root => FindNodeByDescriptionContains(root, "copies")),
            ("Expand handle", root => FindNodeByDescriptionContains(root, "expand")),
        };

        foreach (var (label, find) in candidates)
        {
            // Ré-interroge l'écran à chaque piste : un clic précédent (même sans succès pour trouver
            // le champ copies) peut avoir changé l'arborescence, il ne faut pas chercher sur un état figé.
            var freshRoot = RootInActiveWindow ?? mainScreenRoot;
            var trigger = find(freshRoot);
            if (trigger is null)
            {
                Android.Util.Log.Debug(LogTag, $"Piste '{label}' : élément introuvable, piste suivante.");
                continue;
            }

            var opened = trigger.PerformAction(Android.Views.Accessibility.Action.Click);
            Android.Util.Log.Debug(LogTag, opened
                ? $"Piste '{label}' cliquée, recherche du champ copies..."
                : $"Piste '{label}' trouvée mais le clic a échoué.");

            if (!opened)
                continue;

            var copiesField = await PollForCopiesFieldAsync();
            if (copiesField is not null)
            {
                var arguments = new Bundle();
                // Clé brute de l'argument Android (ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE), confirmée
                // au caractère près contre le vrai code source d'AccessibilityNodeInfo.java.
                arguments.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", copies.ToString());
                var applied = copiesField.PerformAction(Android.Views.Accessibility.Action.SetText, arguments);

                Android.Util.Log.Debug(LogTag, applied
                    ? $"Nombre de copies réglé sur {copies} (via '{label}')."
                    : $"Champ copies trouvé via '{label}' mais le réglage a échoué.");
                return;
            }

            Android.Util.Log.Debug(LogTag, $"Piste '{label}' : aucun champ copies apparu après le clic.");
        }

        Android.Util.Log.Error(LogTag, "Impossible de régler le nombre de copies : aucune des pistes connues n'a fonctionné. Arborescence actuelle :");
        var finalRoot = RootInActiveWindow;
        if (finalRoot is not null)
            DumpTree(finalRoot, 0);
    }

    private async Task<AccessibilityNodeInfo?> PollForCopiesFieldAsync()
    {
        for (int poll = 0; poll < 10; poll++)
        {
            await Task.Delay(200);

            var currentRoot = RootInActiveWindow;
            if (currentRoot is null)
                continue;

            var field = currentRoot
                .FindAccessibilityNodeInfosByViewId("com.android.printspooler:id/copies_edittext")
                ?.FirstOrDefault()
                ?? FindFirstEditable(currentRoot);

            if (field is not null)
                return field;
        }

        return null;
    }

    private static AccessibilityNodeInfo? FindNodeByDescriptionContains(AccessibilityNodeInfo node, string keyword)
    {
        var desc = node.ContentDescription?.ToString();
        if (desc is not null && desc.Contains(keyword, StringComparison.OrdinalIgnoreCase) && node.Clickable)
            return node;

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child is null)
                continue;

            var found = FindNodeByDescriptionContains(child, keyword);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static AccessibilityNodeInfo? FindFirstEditable(AccessibilityNodeInfo node)
    {
        if (node.Editable)
            return node;

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child is null)
                continue;

            var found = FindFirstEditable(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Journalise l'arborescence complète de l'écran (classe, texte, description, éditable/cliquable)
    /// pour permettre de diagnostiquer précisément sa structure réelle si la recherche automatique échoue.
    /// </summary>
    private static void DumpTree(AccessibilityNodeInfo node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var className = node.ClassName?.ToString() ?? "?";
        var text = node.Text?.ToString();
        var desc = node.ContentDescription?.ToString();
        Android.Util.Log.Debug(LogTag, $"{indent}{className} text='{text}' desc='{desc}' editable={node.Editable} clickable={node.Clickable}");

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child is not null)
                DumpTree(child, depth + 1);
        }
    }

    /// <summary>
    /// Cherche récursivement un nœud dont le texte ou la description contient l'un des mots-clés,
    /// puis remonte à son premier ancêtre cliquable (le libellé/icône visible n'est souvent pas
    /// lui-même la cible cliquable, mais son conteneur parent l'est).
    /// </summary>
    private static AccessibilityNodeInfo? FindClickableByText(AccessibilityNodeInfo node, params string[] keywords)
    {
        var label = (node.ContentDescription?.ToString() ?? node.Text?.ToString() ?? string.Empty).ToLowerInvariant();
        if (keywords.Any(label.Contains))
        {
            var candidate = node;
            while (candidate is not null && !candidate.Clickable)
                candidate = candidate.Parent;
            if (candidate is not null)
                return candidate;
        }

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            if (child is null)
                continue;

            var found = FindClickableByText(child, keywords);
            if (found is not null)
                return found;
        }

        return null;
    }
}
