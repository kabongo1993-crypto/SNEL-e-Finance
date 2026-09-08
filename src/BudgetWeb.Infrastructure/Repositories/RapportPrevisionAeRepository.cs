using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class RapportPrevisionAeRepository : IRapportPrevisionAeRepository
{
    private readonly BudgetDbContext _context;
    private readonly IClassementAeRepository _classementAeRepository;
    private readonly IRapportPrevisionDcRepository _dcRepository;

    public RapportPrevisionAeRepository(
        BudgetDbContext context,
        IClassementAeRepository classementAeRepository,
        IRapportPrevisionDcRepository dcRepository)
    {
        _context = context;
        _classementAeRepository = classementAeRepository;
        _dcRepository = dcRepository;
    }

    public Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
        long idVersion, CancellationToken cancellationToken = default)
        => _dcRepository.GetVersionInfoAsync(idVersion, cancellationToken);

    public Task<RapportStructureNode?> GetStructureAsync(long idStructure, CancellationToken cancellationToken = default)
        => _dcRepository.GetStructureAsync(idStructure, cancellationToken);

    public Task<IReadOnlyList<RapportUbOrgRow>> GetUbsActivesAsync(CancellationToken cancellationToken = default)
        => _dcRepository.GetUbsActivesAsync(cancellationToken);

    public Task<IReadOnlyDictionary<long, RapportStructureNode>> GetStructuresAsync(
        IReadOnlyList<long> structureIds,
        CancellationToken cancellationToken = default)
        => _dcRepository.GetStructuresAsync(structureIds, cancellationToken);

    public Task<IReadOnlySet<long>> GetUbIdsParStatutAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        string statut,
        CancellationToken cancellationToken = default)
        => _dcRepository.GetUbIdsParStatutAsync(idVersion, ubIds, statut, cancellationToken);

    public Task<IReadOnlyDictionary<long, decimal[]>> GetRepartitionsMensuellesAsync(
        IReadOnlyList<long> idPrevisions,
        CancellationToken cancellationToken = default)
        => _dcRepository.GetRepartitionsMensuellesAsync(idPrevisions, cancellationToken);

    public Task<IReadOnlyList<ClassementAeLigneDto>> GetClassementAeAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
        => _classementAeRepository.GetByVersionUbAsync(idVersion, idUB, cancellationToken);

    public Task<bool> ExistsClassementAeAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default)
        => _classementAeRepository.ExistsAnyAsync(idVersion, idUB, cancellationToken);

    public async Task<IReadOnlyList<RapportAePrevisionRow>> GetPrevisionsAeAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ubIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && ids.Contains(p.FK_UniteBudgetaire)
                        && p.TypeBudget.CodeType == TypeBudgetCode.ActionsExploitation
                        && p.LibelleItemAE != null
                        && p.LibelleItemAE != ""
                        && p.FK_RubriqueBudgetaire != null)
            .Select(p => new RapportAePrevisionRow(
                p.IdPrevision,
                p.FK_UniteBudgetaire,
                p.LibelleItemAE!,
                p.MontantAnnuel,
                p.ModePrevision.CodeMode,
                p.FK_GroupeItemAE,
                p.GroupeItemAE != null ? p.GroupeItemAE.Libelle : null,
                p.FK_RubriqueBudgetaire!.Value,
                p.RubriqueBudgetaire!.CodeRB,
                p.RubriqueBudgetaire.Libelle,
                p.RubriqueBudgetaire.FK_GroupeRubriqueBudgetaire,
                p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.CodeGroupe
                    : null,
                p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.Libelle
                    : null,
                p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? (int?)p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.OrdreAffichage
                    : null))
            .ToListAsync(cancellationToken);
    }
}
