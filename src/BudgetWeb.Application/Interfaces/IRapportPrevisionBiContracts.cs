using BudgetWeb.Application.DTOs.Rapports;
using BudgetWeb.Application.Rapports.Common;

namespace BudgetWeb.Application.Interfaces;

public interface IRapportPrevisionBiService
{
    Task<RapportBiDto> GetAsync(RapportBiQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> GetPdfAsync(RapportBiQuery query, CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionBiRepository
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

    Task<IReadOnlyList<RapportBiPrevisionRow>> GetPrevisionsBiAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, decimal[]>> GetRepartitionsMensuellesAsync(
        IReadOnlyList<long> idPrevisions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RapportBiItemCatalogueRow>> GetItemsBiAsync(CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionBiPdfRenderer
{
    byte[] Render(RapportBiDto rapport);
}

public record RapportBiPrevisionRow(
    long IdPrevision,
    long IdUB,
    long IdItemBI,
    string CodeItem,
    string LibelleItem,
    long? IdItemParent,
    int NiveauItem,
    string? Categorie,
    string DetailBI,
    decimal MontantAnnuel,
    string CodeMode);

public record RapportBiItemCatalogueRow(
    long IdItemBI,
    string CodeItem,
    string Libelle,
    long? ParentId,
    int Niveau,
    string? Categorie,
    bool Actif);
