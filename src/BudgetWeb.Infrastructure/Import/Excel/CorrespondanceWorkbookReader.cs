using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using ClosedXML.Excel;

namespace BudgetWeb.Infrastructure.Import.Excel;

public class CorrespondanceWorkbookReader : ICorrespondanceWorkbookReader
{
    private const string SheetEntites = "01_ENTITES";
    private const string SheetDepartements = "02_DEPARTEMENTS";
    private const string SheetEntiteDepartement = "03_ENTITE_DEPARTEMENT";
    private const string SheetUb = "04_UB_A_IMPORTER";
    private const string SheetElementsRattaches = "05_ELEMENTS_RATTACHES";
    private const string SheetSansCodeUb = "06_SANS_CODE_UB";
    private const string SheetArbre = "07_ARBRE_SOURCE";

    public Task<CorrespondanceWorkbookData> LireAsync(string fichierSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(fichierSource);

        var data = new CorrespondanceWorkbookData(
            Path.GetFullPath(fichierSource),
            LireEntites(workbook.Worksheet(SheetEntites)),
            LireDepartements(workbook.Worksheet(SheetDepartements)),
            LireRelations(workbook.Worksheet(SheetEntiteDepartement)),
            LireUb(workbook.Worksheet(SheetUb)),
            LireElementsRattaches(workbook.Worksheet(SheetElementsRattaches)),
            LireSansCodeUb(workbook.Worksheet(SheetSansCodeUb)),
            LireArbre(workbook.Worksheet(SheetArbre)));

        return Task.FromResult(data);
    }

    private static IReadOnlyList<EntiteSheetRow> LireEntites(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new EntiteSheetRow(
            Cell(r, 3),
            Cell(r, 4),
            Cell(r, 5))).Where(e => !string.IsNullOrWhiteSpace(e.Sigle)).ToList();

    private static IReadOnlyList<DepartementSheetRow> LireDepartements(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new DepartementSheetRow(
            Cell(r, 1),
            Cell(r, 2))).Where(d => !string.IsNullOrWhiteSpace(d.Sigle)).ToList();

    private static IReadOnlyList<EntiteDepartementSheetRow> LireRelations(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new EntiteDepartementSheetRow(
            Cell(r, 1),
            Cell(r, 2),
            Cell(r, 3),
            Cell(r, 4))).Where(x => !string.IsNullOrWhiteSpace(x.SigleEntite) && !string.IsNullOrWhiteSpace(x.SigleDepartement)).ToList();

    private static IReadOnlyList<UbImportSheetRow> LireUb(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new UbImportSheetRow(
            Cell(r, 1),
            Cell(r, 2),
            Cell(r, 3),
            Cell(r, 4),
            Cell(r, 5),
            Cell(r, 6),
            ParseInt(Cell(r, 13)))).Where(u => !string.IsNullOrWhiteSpace(u.CodeUB)).ToList();

    private static IReadOnlyList<ElementRattacheSheetRow> LireElementsRattaches(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new ElementRattacheSheetRow(
            Cell(r, 1),
            Cell(r, 3),
            Cell(r, 5),
            Cell(r, 6),
            NullIfEmpty(Cell(r, 8)),
            NullIfEmpty(Cell(r, 9)),
            ParseInt(Cell(r, 10)),
            Cell(r, 14),
            ParseInt(Cell(r, 13)))).Where(x => !string.IsNullOrWhiteSpace(x.CodeUB)).ToList();

    private static IReadOnlyList<SansCodeUbSheetRow> LireSansCodeUb(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new SansCodeUbSheetRow(
            Cell(r, 1),
            Cell(r, 3),
            Cell(r, 5),
            NullIfEmpty(Cell(r, 7)),
            NullIfEmpty(Cell(r, 8)),
            Cell(r, 13),
            ParseInt(Cell(r, 12)))).Where(x => !string.IsNullOrWhiteSpace(x.SigleEntite)).ToList();

    private static IReadOnlyList<ArbreSourceSheetRow> LireArbre(IXLWorksheet ws)
        => ws.RowsUsed().Skip(1).Select(r => new ArbreSourceSheetRow(
            ParseInt(Cell(r, 1)),
            Cell(r, 2),
            Cell(r, 3),
            Cell(r, 4),
            Cell(r, 5),
            Cell(r, 6),
            Cell(r, 7),
            ParseInt(Cell(r, 8)),
            Cell(r, 9),
            NullIfEmpty(Cell(r, 10)),
            NullIfEmpty(Cell(r, 11)),
            NullIfEmpty(Cell(r, 12)),
            NullIfEmpty(Cell(r, 13)))).Where(x => x.LigneSource > 0).ToList();

    private static string Cell(IXLRow row, int index)
        => row.Cell(index).GetString().Trim();

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ParseInt(string value)
        => int.TryParse(value, out var result) ? result : 0;
}
