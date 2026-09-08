using BudgetWeb.Domain.Referentiels;



namespace BudgetWeb.Domain.DemandePaiement;



/// <summary>Règles d'éligibilité au billet de conversion.</summary>

public static class BilletConversionRules

{

    /// <summary>

    /// True si le billet est requis : mode CAISSE et devise sollicitée ≠ CDF.

    /// </summary>

    public static bool NecessiteBillet(string? modePaiement, string? deviseDemande)

    {

        var mode = ModePaiementDpm.Normaliser(modePaiement);

        if (!string.Equals(mode, ModePaiementDpm.Caisse, StringComparison.Ordinal))

            return false;



        var code = DemandePaiementMontants.NormaliserCodeDevise(deviseDemande);

        return !string.Equals(code, TauxChangeConventions.DeviseCdf, StringComparison.OrdinalIgnoreCase);

    }

}


