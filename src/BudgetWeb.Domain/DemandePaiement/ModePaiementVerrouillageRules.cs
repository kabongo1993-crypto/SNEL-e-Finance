using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>
/// Verrouillage du mode de paiement après établissement d'un document DPM (ETABLI uniquement).
/// </summary>
public static class ModePaiementVerrouillageRules
{
    public const string MotifBilletConversion = "BILLET_CONVERSION";
    public const string MotifPieceCaisse = "PIECE_CAISSE";
    public const string MotifBonProvisoire = "BON_PROVISOIRE";
    public const string MotifMinuteCheque = "MINUTE_CHEQUE";

    /// <summary>Null si le mode reste modifiable.</summary>
    public static string? MotifVerrouillage(Entities.DemandePaiement demande)
    {
        if (demande.BilletConversion is { } billet
            && string.Equals(
                StatutBilletConversion.Normaliser(billet.Statut),
                StatutBilletConversion.Etabli,
                StringComparison.Ordinal))
        {
            return MotifBilletConversion;
        }

        if (demande.PieceCaisse is { } piece
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(piece.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal))
        {
            return MotifPieceCaisse;
        }

        if (demande.BonProvisoire is { } bon
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(bon.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal))
        {
            return MotifBonProvisoire;
        }

        if (demande.MinuteCheque is { } minute
            && string.Equals(
                StatutDocumentInstrumentPaiement.Normaliser(minute.Statut),
                StatutDocumentInstrumentPaiement.Etabli,
                StringComparison.Ordinal))
        {
            return MotifMinuteCheque;
        }

        return null;
    }

    public static bool EstVerrouille(Entities.DemandePaiement demande)
        => MotifVerrouillage(demande) is not null;

    public static void ExigerModifiable(Entities.DemandePaiement demande)
    {
        var motif = MotifVerrouillage(demande);
        if (motif is null)
            return;

        var message = motif switch
        {
            MotifBilletConversion =>
                "Le mode de paiement est verrouillé : le billet de conversion a déjà été établi.",
            MotifPieceCaisse =>
                "Le mode de paiement est verrouillé : la pièce de caisse a déjà été établie.",
            MotifBonProvisoire =>
                "Le mode de paiement est verrouillé : le bon provisoire a déjà été établi.",
            MotifMinuteCheque =>
                "Le mode de paiement est verrouillé : la minute de chèque a déjà été établie.",
            _ => "Le mode de paiement est verrouillé : un document de paiement a déjà été établi.",
        };

        throw new InvalidOperationException(message);
    }

    public static void ExigerModificationModeAutorisee(Entities.DemandePaiement demande, string? modeCible)
    {
        if (string.IsNullOrWhiteSpace(modeCible))
            return;

        var actuel = ModePaiementDpm.Normaliser(demande.ModePaiementSollicite);
        var nouveau = ModePaiementDpm.Normaliser(modeCible);
        if (!string.Equals(actuel, nouveau, StringComparison.Ordinal))
            ExigerModifiable(demande);
    }
}
