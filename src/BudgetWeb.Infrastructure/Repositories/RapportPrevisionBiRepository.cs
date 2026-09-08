using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Rapports.Common;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class RapportPrevisionBiRepository : IRapportPrevisionBiRepository
{
    private readonly BudgetDbContext _context;

    public RapportPrevisionBiRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
        long idVersion, CancellationToken cancellationToken = default)
    {
        var row = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => v.IdVersion == idVersion)
            .Select(v => new { Annee = v.ExerciceBudgetaire.Annee, v.NumeroVersion, v.Libelle })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Annee, row.NumeroVersion, row.Libelle);
    }

    public async Task<RapportStructureNode?> GetStructureAsync(long idStructure, CancellationToken cancellationToken = default)
    {
        return await _context.StructuresOrganisationnelles.AsNoTracking()
            .Where(s => s.IdStructure == idStructure)
            .Select(s => new RapportStructureNode(
                s.IdStructure,
                s.FK_StructureOrganisationnelleParent,
                s.TypeStructure,
                s.Code,
                s.Libelle))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RapportUbOrgRow>> GetUbsActivesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.Actif)
            .OrderBy(u => u.CodeUB)
            .Select(u => new RapportUbOrgRow(
                u.IdUB,
                u.CodeUB,
                u.Libelle,
                u.FK_Departement,
                u.Departement.Code,
                u.Departement.Libelle,
                u.FK_StructureOrganisationnelle,
                u.StructureOrganisationnelle.TypeStructure,
                u.StructureOrganisationnelle.Code,
                u.StructureOrganisationnelle.Libelle))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, RapportStructureNode>> GetStructuresAsync(
        IReadOnlyList<long> structureIds,
        CancellationToken cancellationToken = default)
    {
        var ids = structureIds.Distinct().ToList();
        var q = _context.StructuresOrganisationnelles.AsNoTracking();
        if (ids.Count > 0)
        {
            q = q.Where(s => ids.Contains(s.IdStructure));
        }

        var rows = await q
            .Select(s => new RapportStructureNode(
                s.IdStructure,
                s.FK_StructureOrganisationnelleParent,
                s.TypeStructure,
                s.Code,
                s.Libelle))
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.IdStructure);
    }

    public async Task<IReadOnlySet<long>> GetUbIdsParStatutAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        string statut,
        CancellationToken cancellationToken = default)
    {
        var ids = ubIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<long>();

        var list = await _context.WorkflowsPrevisionUb.AsNoTracking()
            .Where(w => w.FK_VersionBudgetaire == idVersion
                        && ids.Contains(w.FK_UniteBudgetaire)
                        && w.Statut == statut)
            .Select(w => w.FK_UniteBudgetaire)
            .ToListAsync(cancellationToken);
        return list.ToHashSet();
    }

    public async Task<IReadOnlyList<RapportBiPrevisionRow>> GetPrevisionsBiAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ubIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion
                        && ids.Contains(p.FK_UniteBudgetaire)
                        && p.TypeBudget.CodeType == TypeBudgetCode.BudgetInvestissement
                        && p.FK_ItemBI != null)
            .Select(p => new RapportBiPrevisionRow(
                p.IdPrevision,
                p.FK_UniteBudgetaire,
                p.FK_ItemBI!.Value,
                p.ItemBI!.CodeItem,
                p.ItemBI.Libelle,
                p.ItemBI.FK_ItemBIParent,
                p.ItemBI.Niveau,
                p.ItemBI.Categorie,
                p.DetailBI ?? string.Empty,
                p.MontantAnnuel,
                p.ModePrevision.CodeMode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal[]>> GetRepartitionsMensuellesAsync(
        IReadOnlyList<long> idPrevisions,
        CancellationToken cancellationToken = default)
    {
        var ids = idPrevisions.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, decimal[]>();

        var rows = await _context.RepartitionsMensuelles.AsNoTracking()
            .Where(r => ids.Contains(r.FK_PrevisionBudgetaire))
            .Select(r => new { r.FK_PrevisionBudgetaire, r.Mois, r.Montant })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<long, decimal[]>();
        foreach (var g in rows.GroupBy(r => r.FK_PrevisionBudgetaire))
        {
            var arr = new decimal[12];
            foreach (var r in g)
            {
                if (r.Mois is >= 1 and <= 12)
                {
                    arr[r.Mois - 1] = r.Montant;
                }
            }

            map[g.Key] = arr;
        }

        return map;
    }

    public async Task<IReadOnlyList<RapportBiItemCatalogueRow>> GetItemsBiAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ItemsBI.AsNoTracking()
            .Where(i => i.Actif)
            .OrderBy(i => i.Niveau)
            .ThenBy(i => i.CodeItem)
            .Select(i => new RapportBiItemCatalogueRow(
                i.IdItemBI,
                i.CodeItem,
                i.Libelle,
                i.FK_ItemBIParent,
                i.Niveau,
                i.Categorie,
                i.Actif))
            .ToListAsync(cancellationToken);
    }
}
