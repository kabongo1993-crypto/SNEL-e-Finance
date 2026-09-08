using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Domain.DemandePaiement;

/// <summary>Libellés métier DPM (alignés sur le frontend e-Finance).</summary>
public static class DemandePaiementStatutLabels
{
    public static string Libelle(string? statut)
    {
        var s = StatutDemandePaiement.Normaliser(statut);
        return s switch
        {
            StatutDemandePaiement.Brouillon => "Brouillon",
            StatutDemandePaiement.EnValidationN1 => "En validation N1",
            StatutDemandePaiement.EnValidationN2 => "En validation N2",
            StatutDemandePaiement.ValideeEntite => "Validée entité",
            StatutDemandePaiement.Soumise => "Soumise",
            StatutDemandePaiement.EnTraitementDpm => "En traitement DPM",
            StatutDemandePaiement.EnControleBudgetaire => "En contrôle budgétaire",
            StatutDemandePaiement.ACorriger => "À corriger",
            StatutDemandePaiement.ViseeBudgetairement => "Visée budgétairement",
            _ => string.IsNullOrWhiteSpace(s) ? "—" : s,
        };
    }
}
