using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Rapports.Common;

namespace BudgetWeb.Application.Interfaces;

public interface IRapportPrevisionDcService
{
    Task<RapportDcDto> GetAsync(RapportDcQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> GetPdfAsync(RapportDcQuery query, CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionDcRepository
{
    Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
        long idVersion, CancellationToken cancellationToken = default);

    Task<RapportStructureNode?> GetStructureAsync(long idStructure, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RapportUbOrgRow>> GetUbsActivesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, RapportStructureNode>> GetStructuresAsync(
        IReadOnlyList<long> structureIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<long>> GetUbIdsParStatutAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        string statut,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RapportDcPrevisionRow>> GetPrevisionsDcAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, decimal[]>> GetRepartitionsMensuellesAsync(
        IReadOnlyList<long> idPrevisions,
        CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionDcPdfRenderer
{
    byte[] Render(RapportDcDto rapport);

    IReadOnlyList<string> RenderPreviewImages(RapportDcDto rapport, string outputDirectory);
}

public record RapportDcPrevisionRow(
    long IdPrevision,
    long IdUB,
    decimal MontantAnnuel,
    string CodeMode,
    long IdRB,
    string CodeRB,
    string LibelleRB,
    long? IdGroupeRB,
    string? CodeGroupe,
    string? LibelleGroupe,
    int? OrdreAffichageGroupe);
