using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Enums;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using ClosedXML.Excel;

namespace BudgetWeb.Infrastructure.Import.Excel;

public class ExcelReferentielReader : IExcelReferentielReader
{
    private static readonly HashSet<string> FeuillesMeta = new(StringComparer.OrdinalIgnoreCase)
    {
        "Correspondance",
        "Légende",
        "Legende",
        "Instructions",
        "README",
        "Sommaire",
        "Index"
    };

    public Task<IReadOnlyList<LigneExcelBruteDto>> LireAsync(string fichierSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(fichierSource);
        var mappingsParFeuille = LireCorrespondance(workbook);
        var lignes = new List<LigneExcelBruteDto>();

        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FeuillesMeta.Contains(worksheet.Name))
            {
                continue;
            }

            if (worksheet.Name.Equals("Correspondance", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var mappingFeuille = mappingsParFeuille.TryGetValue(worksheet.Name, out var map)
                ? map
                : null;

            LireFeuilleDonnees(worksheet, mappingFeuille, lignes);
        }

        return Task.FromResult<IReadOnlyList<LigneExcelBruteDto>>(lignes);
    }

    private static Dictionary<string, Dictionary<string, string>> LireCorrespondance(XLWorkbook workbook)
    {
        var resultat = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var ws = workbook.Worksheets.FirstOrDefault(w =>
            w.Name.Equals("Correspondance", StringComparison.OrdinalIgnoreCase));

        if (ws is null)
        {
            return resultat;
        }

        var headerRow = ws.FirstRowUsed();
        if (headerRow is null)
        {
            return resultat;
        }

        var headers = headerRow.CellsUsed().ToDictionary(
            c => c.Address.ColumnNumber,
            c => c.GetString().Trim(),
            comparer: EqualityComparer<int>.Default);

        int? colFeuille = TrouverColonne(headers, "Feuille", "Sheet", "Onglet");
        int? colColonne = TrouverColonne(headers, "Colonne", "Colonne Excel", "Column", "Colonne source", "Colonne Source");
        int? colChamp = TrouverColonne(headers, "Champ", "Champ Budget Web", "Field", "Champ cible", "Champ Cible");

        if (colFeuille is null || colColonne is null || colChamp is null)
        {
            return resultat;
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var feuille = row.Cell(colFeuille.Value).GetString().Trim();
            var colonne = row.Cell(colColonne.Value).GetString().Trim();
            var champ = row.Cell(colChamp.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(feuille) || string.IsNullOrWhiteSpace(colonne) || string.IsNullOrWhiteSpace(champ))
            {
                continue;
            }

            if (!resultat.TryGetValue(feuille, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                resultat[feuille] = map;
            }

            map[colonne] = champ;
        }

        return resultat;
    }

    private static void LireFeuilleDonnees(
        IXLWorksheet worksheet,
        Dictionary<string, string>? mappingCorrespondance,
        List<LigneExcelBruteDto> lignes)
    {
        var firstRow = worksheet.FirstRowUsed();
        if (firstRow is null)
        {
            return;
        }

        var headerCells = firstRow.CellsUsed().ToList();
        if (headerCells.Count == 0)
        {
            return;
        }

        var colonnes = headerCells.ToDictionary(
            c => c.GetString().Trim(),
            c => c.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            if (LigneEstVide(row, colonnes.Values))
            {
                continue;
            }

            var valeurs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var colonne in colonnes)
            {
                var raw = row.Cell(colonne.Value).GetString();
                valeurs[colonne.Key] = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            }

            if (mappingCorrespondance is not null)
            {
                var remapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in valeurs)
                {
                    if (mappingCorrespondance.TryGetValue(pair.Key, out var champ))
                    {
                        remapped[champ] = pair.Value;
                    }
                    else
                    {
                        remapped[pair.Key] = pair.Value;
                    }
                }

                valeurs = remapped;
            }

            lignes.Add(new LigneExcelBruteDto(row.RowNumber(), worksheet.Name, valeurs));
        }
    }

    private static bool LigneEstVide(IXLRow row, IEnumerable<int> colonnes)
    {
        foreach (var col in colonnes)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(col).GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private static int? TrouverColonne(IReadOnlyDictionary<int, string> headers, params string[] noms)
    {
        foreach (var header in headers)
        {
            if (noms.Any(n => header.Value.Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                return header.Key;
            }
        }

        return null;
    }
}
