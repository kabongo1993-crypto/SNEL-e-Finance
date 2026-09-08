using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Règles d'éligibilité et cohérence des documents instrument de paiement.</summary>
public static class DocumentInstrumentPaiementRules
{
    public static bool NecessiteDocumentInstrument(string? modePaiement)
    {
        var mode = ModePaiementDpm.Normaliser(modePaiement);
        return mode is ModePaiementDpm.Caisse or ModePaiementDpm.Banque;
    }

    public static void ExigerModeCaisse(string? modePaiement, string documentLabel)
    {
        if (!string.Equals(ModePaiementDpm.Normaliser(modePaiement), ModePaiementDpm.Caisse, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Le document « {documentLabel} » n'est applicable qu'en mode CAISSE.");
        }
    }

    public static void ExigerModeBanque(string? modePaiement, string documentLabel)
    {
        if (!string.Equals(ModePaiementDpm.Normaliser(modePaiement), ModePaiementDpm.Banque, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Le document « {documentLabel} » n'est applicable qu'en mode BANQUE.");
        }
    }

    public static void ExigerBilletSiRequis(Entities.DemandePaiement demande)
    {
        if (!BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise))
            return;

        if (demande.BilletConversion is not { } billet
            || !string.Equals(
                StatutBilletConversion.Normaliser(billet.Statut),
                StatutBilletConversion.Etabli,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Le billet de conversion doit être établi avant le document de caisse.");
        }
    }

    public static void ExigerAucunDocumentInstrumentConflit(Entities.DemandePaiement demande, string typeAutorise)
    {
        var type = TypeInstrumentPaiement.Normaliser(typeAutorise);

        if (type != TypeInstrumentPaiement.PieceCaisse
            && EstEtabli(demande.PieceCaisse))
        {
            throw new InvalidOperationException(
                "Une pièce de caisse est déjà établie pour cette demande.");
        }

        if (type != TypeInstrumentPaiement.BonProvisoire
            && EstEtabli(demande.BonProvisoire))
        {
            throw new InvalidOperationException(
                "Un bon provisoire est déjà établi pour cette demande.");
        }

        if (type != TypeInstrumentPaiement.MinuteCheque
            && EstEtabli(demande.MinuteCheque))
        {
            throw new InvalidOperationException(
                "Une minute de chèque est déjà établie pour cette demande.");
        }
    }

    public static bool DocumentInstrumentEtabli(Entities.DemandePaiement demande)
        => EstEtabli(demande.PieceCaisse)
           || EstEtabli(demande.BonProvisoire)
           || EstEtabli(demande.MinuteCheque);

    public static decimal ResoudreMontantFc(Entities.DemandePaiement demande)
    {
        if (BilletConversionRules.NecessiteBillet(demande.ModePaiementSollicite, demande.Devise))
        {
            if (demande.BilletConversion is null)
            {
                throw new InvalidOperationException(
                    "Le billet de conversion est requis pour déterminer le montant en FC.");
            }

            return demande.BilletConversion.MontantCdf;
        }

        var devise = DemandePaiementMontants.NormaliserCodeDevise(demande.Devise);
        if (string.Equals(devise, TauxChangeConventions.DeviseCdf, StringComparison.OrdinalIgnoreCase))
            return demande.MontantBrut;

        if (demande.MontantPaiement is decimal mp
            && string.Equals(
                DemandePaiementMontants.NormaliserCodeDevise(demande.DevisePaiement),
                TauxChangeConventions.DeviseCdf,
                StringComparison.OrdinalIgnoreCase))
        {
            return mp;
        }

        throw new InvalidOperationException(
            "Impossible de déterminer le montant payable en FC pour cette demande.");
    }

    public static (decimal Montant, string Devise) ResoudreMontantPaiementBanque(Entities.DemandePaiement demande)
    {
        if (demande.MontantPaiement is decimal mp && !string.IsNullOrWhiteSpace(demande.DevisePaiement))
            return (mp, DemandePaiementMontants.NormaliserCodeDevise(demande.DevisePaiement));

        return (demande.MontantBrut, DemandePaiementMontants.NormaliserCodeDevise(demande.Devise));
    }

    private static bool EstEtabli(PieceCaisse? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);

    private static bool EstEtabli(BonProvisoire? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);

    private static bool EstEtabli(MinuteCheque? doc)
        => doc is not null
           && string.Equals(
               StatutDocumentInstrumentPaiement.Normaliser(doc.Statut),
               StatutDocumentInstrumentPaiement.Etabli,
               StringComparison.Ordinal);
}
