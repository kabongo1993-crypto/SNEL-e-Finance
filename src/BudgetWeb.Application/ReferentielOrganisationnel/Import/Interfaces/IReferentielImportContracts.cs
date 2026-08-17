using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;

public interface IExcelReferentielReader
{
    Task<IReadOnlyList<LigneExcelBruteDto>> LireAsync(string fichierSource, CancellationToken cancellationToken = default);
}

public interface IReferentielImportService
{
    Task<ImportAnalyseResultDto> AnalyserAsync(string? fichierSource, CancellationToken cancellationToken = default);
    Task<ImportPreviewDto> PrevisualiserAsync(string? fichierSource, CancellationToken cancellationToken = default);
    Task<ImportExecuteResultDto> ExecuterAsync(ImportExecuteRequestDto request, CancellationToken cancellationToken = default);
}

public interface IReferentielImportRepository
{
    Task<ImportCompteursDto> ImporterAsync(ImportPreviewDto preview, CancellationToken cancellationToken = default);
    Task<bool> ReferentielOrganisationnelEstVideAsync(CancellationToken cancellationToken = default);
}
