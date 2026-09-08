using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Rapports.Common;

namespace BudgetWeb.Application.Interfaces;

public interface IRapportPrevisionAeService
{
    Task<RapportAeDto> GetAsync(RapportAeQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> GetPdfAsync(RapportAeQuery query, CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionAeRepository
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

    Task<IReadOnlyList<RapportAePrevisionRow>> GetPrevisionsAeAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, decimal[]>> GetRepartitionsMensuellesAsync(
        IReadOnlyList<long> idPrevisions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassementAeLigneDto>> GetClassementAeAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsClassementAeAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionAePdfRenderer
{
    byte[] Render(RapportAeDto rapport);

    IReadOnlyList<string> RenderPreviewImages(RapportAeDto rapport, string outputDirectory);
}

public record RapportAePrevisionRow(
    long IdPrevision,
    long IdUB,
    string LibelleItemAE,
    decimal MontantAnnuel,
    string CodeMode,
    long? IdGroupeItemAE,
    string? LibelleGroupe,
    long IdRB,
    string CodeRB,
    string LibelleRB,
    long? IdGroupeRB,
    string? CodeGroupeRB,
    string? LibelleGroupeRB,
    int? OrdreAffichageGroupe);
