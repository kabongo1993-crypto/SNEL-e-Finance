using System.Globalization;
using BudgetWeb.Application.SnelComptes.Import.DTOs;
using BudgetWeb.Application.SnelComptes.Import.Interfaces;
using ClosedXML.Excel;

namespace BudgetWeb.Infrastructure.Import.Excel;

public class SnelComptesWorkbookReader : ISnelComptesWorkbookReader
{
    public const string NomFeuille = "Comptes SYSCOHADA";

    public Task<IReadOnlyList<SnelComptesExcelRow>> LireAsync(
        string fichierSource,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(fichierSource);
        if (!workbook.TryGetWorksheet(NomFeuille, out var worksheet))
        {
            throw new InvalidOperationException(
                $"La feuille « {NomFeuille} » est introuvable dans le fichier Excel.");
        }

        var last = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var rows = new List<SnelComptesExcelRow>(Math.Max(0, last));
        for (var r = 1; r <= last; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new SnelComptesExcelRow(
                r,
                Cell(worksheet, r, 3),
                Cell(worksheet, r, 4),
                Cell(worksheet, r, 5),
                Cell(worksheet, r, 6),
                Cell(worksheet, r, 7)));
        }

        return Task.FromResult<IReadOnlyList<SnelComptesExcelRow>>(rows);
    }

    private static string Cell(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.Number)
        {
            var number = cell.GetDouble();
            if (number == Math.Truncate(number))
            {
                return ((long)number).ToString(CultureInfo.InvariantCulture);
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }
}
