using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

/// <summary>Règle unique d'agrégation du statut VERSION à partir des statuts UB.</summary>
public static class VersionStatutAgrege
{
    public static string Calculer(IEnumerable<string> statutsUb)
    {
        var list = statutsUb
            .Select(StatutVersionBudgetaire.Normaliser)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (list.Count == 0)
        {
            return StatutVersionBudgetaire.Brouillon;
        }

        if (list.Any(s => s == StatutVersionBudgetaire.Rejetee))
        {
            return StatutVersionBudgetaire.Rejetee;
        }

        if (list.All(s => s == StatutVersionBudgetaire.Validee))
        {
            return StatutVersionBudgetaire.Validee;
        }

        static int Rank(string s) => s switch
        {
            StatutVersionBudgetaire.Validee => 4,
            StatutVersionBudgetaire.Controlee => 3,
            StatutVersionBudgetaire.Soumise => 2,
            StatutVersionBudgetaire.Brouillon => 1,
            _ => 0,
        };

        if (list.All(s => Rank(s) >= Rank(StatutVersionBudgetaire.Controlee)))
        {
            return StatutVersionBudgetaire.Controlee;
        }

        if (list.All(s => Rank(s) >= Rank(StatutVersionBudgetaire.Soumise)))
        {
            return StatutVersionBudgetaire.Soumise;
        }

        return StatutVersionBudgetaire.Brouillon;
    }
}
