using BudgetWeb.Application.SnelComptes.Import.DTOs;

namespace BudgetWeb.Application.SnelComptes.Import.Interfaces;

public interface ISnelComptesWorkbookReader
{
    Task<IReadOnlyList<SnelComptesExcelRow>> LireAsync(
        string fichierSource,
        CancellationToken cancellationToken = default);
}

public interface ISnelComptesImportTransaction
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}

public interface ISnelComptesImportService
{
    Task<SnelComptesPreviewDto> PrevisualiserAsync(
        string? fichierSource,
        CancellationToken cancellationToken = default);

    Task<SnelComptesExecuteResultDto> ExecuterAsync(
        SnelComptesExecuteRequestDto request,
        CancellationToken cancellationToken = default);
}
