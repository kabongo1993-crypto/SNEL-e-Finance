using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;

namespace BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;

public interface ICorrespondanceWorkbookReader
{
    Task<CorrespondanceWorkbookData> LireAsync(string fichierSource, CancellationToken cancellationToken = default);
}
