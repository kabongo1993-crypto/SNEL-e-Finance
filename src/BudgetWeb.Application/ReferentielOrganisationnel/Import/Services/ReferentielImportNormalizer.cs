using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Models;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Services;

public static class ReferentielImportNormalizer
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [ImportChampExcel.EntiteCode] = ["ENTITE", "Entité", "Entite", "Code Entité", "Code Entite", "EntiteCode"],
        [ImportChampExcel.EntiteLibelle] = ["Libellé Entité", "Libelle Entite", "Libelle Entité", "EntiteLibelle"],
        [ImportChampExcel.DepartementSigle] = ["DEPARTEMENT", "Département", "Departement", "Sigle Département", "Sigle Departement", "Dept", "Code Dept"],
        [ImportChampExcel.DepartementLibelle] = ["Libellé Département", "Libelle Departement", "Libelle Département", "DepartementLibelle"],
        [ImportChampExcel.DirectionCode] = ["DIRECTION", "Direction", "Code Direction", "DirectionCode"],
        [ImportChampExcel.DirectionLibelle] = ["Libellé Direction", "Libelle Direction", "DirectionLibelle"],
        [ImportChampExcel.DivisionCode] = ["DIVISION", "Division", "Code Division", "DivisionCode"],
        [ImportChampExcel.DivisionLibelle] = ["Libellé Division", "Libelle Division", "DivisionLibelle"],
        [ImportChampExcel.ServiceCode] = ["SERVICE", "Service", "Code Service", "ServiceCode"],
        [ImportChampExcel.ServiceLibelle] = ["Libellé Service", "Libelle Service", "ServiceLibelle"],
        [ImportChampExcel.SectionCode] = ["SECTION", "Section", "Code Section", "SectionCode"],
        [ImportChampExcel.SectionLibelle] = ["Libellé Section", "Libelle Section", "SectionLibelle"],
        [ImportChampExcel.CodeUb] = ["Code_UB", "Code UB", "CodeUB", "CODE_UB"],
        [ImportChampExcel.LibelleUb] = ["Libellé UB", "Libelle UB", "LibelleUB", "Libelle_UB", "LIBELLE_UB"]
    };

    public static IReadOnlyDictionary<string, string> ResoudreMappingColonnes(IEnumerable<string> entetes)
        => ResoudreMappingColonnes(entetes.ToDictionary(h => h, h => h, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyDictionary<string, string> ResoudreMappingColonnes(IReadOnlyDictionary<string, string> entetes)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entete in entetes.Keys)
        {
            var champ = ResoudreChamp(entete);
            if (champ is not null)
            {
                mapping[entete] = champ;
            }
        }

        return mapping;
    }

    public static IReadOnlyList<LigneExcelNormaliseeDto> Normaliser(IReadOnlyList<LigneExcelBruteDto> lignesBrutes)
    {
        var resultat = new List<LigneExcelNormaliseeDto>(lignesBrutes.Count);
        string? lastCodeUb = null;

        foreach (var ligne in lignesBrutes.OrderBy(l => l.Feuille).ThenBy(l => l.NumeroLigne))
        {
            var mapping = ResoudreMappingColonnes(ligne.Colonnes.Keys);
            string? Get(string champ)
            {
                foreach (var pair in mapping)
                {
                    if (pair.Value == champ && ligne.Colonnes.TryGetValue(pair.Key, out var value))
                    {
                        return NormaliserValeur(value);
                    }
                }

                return null;
            }

            var codeUb = Get(ImportChampExcel.CodeUb);
            var estSecondaire = string.IsNullOrWhiteSpace(codeUb) && !string.IsNullOrWhiteSpace(lastCodeUb);
            if (!string.IsNullOrWhiteSpace(codeUb))
            {
                lastCodeUb = codeUb;
            }

            resultat.Add(new LigneExcelNormaliseeDto(
                ligne.NumeroLigne,
                ligne.Feuille,
                Get(ImportChampExcel.EntiteCode),
                Get(ImportChampExcel.EntiteLibelle),
                Get(ImportChampExcel.DepartementSigle),
                Get(ImportChampExcel.DepartementLibelle),
                Get(ImportChampExcel.DirectionCode),
                Get(ImportChampExcel.DirectionLibelle),
                Get(ImportChampExcel.DivisionCode),
                Get(ImportChampExcel.DivisionLibelle),
                Get(ImportChampExcel.ServiceCode),
                Get(ImportChampExcel.ServiceLibelle),
                Get(ImportChampExcel.SectionCode),
                Get(ImportChampExcel.SectionLibelle),
                codeUb,
                Get(ImportChampExcel.LibelleUb),
                estSecondaire,
                string.IsNullOrWhiteSpace(codeUb)));
        }

        return resultat;
    }

    private static string? ResoudreChamp(string entete)
    {
        var trimmed = entete.Trim();
        foreach (var alias in Aliases)
        {
            if (alias.Value.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return alias.Key;
            }

            if (string.Equals(alias.Key, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return alias.Key;
            }
        }

        return null;
    }

    private static string? NormaliserValeur(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
