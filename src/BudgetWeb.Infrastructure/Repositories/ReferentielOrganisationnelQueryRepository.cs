using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class ReferentielOrganisationnelQueryRepository : IReferentielOrganisationnelQueryRepository
{
    private readonly BudgetDbContext _context;

    public ReferentielOrganisationnelQueryRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<ReferentielOrganisationnelSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var structures = await _context.StructuresOrganisationnelles
            .AsNoTracking()
            .Select(s => new StructureRow(
                s.IdStructure,
                s.FK_StructureOrganisationnelleParent,
                s.TypeStructure,
                s.Code,
                s.Libelle))
            .ToListAsync(cancellationToken);

        var unites = await _context.UnitesBudgetaires
            .AsNoTracking()
            .Include(u => u.Departement)
            .Include(u => u.StructureOrganisationnelle)
            .OrderBy(u => u.CodeUB)
            .ToListAsync(cancellationToken);

        var byId = structures.ToDictionary(s => s.IdStructure);
        var enfantsParParent = structures
            .Where(s => s.ParentId.HasValue)
            .GroupBy(s => s.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var ubParStructure = unites
            .GroupBy(u => u.FK_StructureOrganisationnelle)
            .ToDictionary(g => g.Key, g => g.Count());

        var structureDtos = structures.Select(s =>
        {
            string? parentCode = null;
            string? parentLibelle = null;
            if (s.ParentId is long parentId && byId.TryGetValue(parentId, out var parent))
            {
                parentCode = ExtraireCodeAffichage(parent.Code);
                parentLibelle = parent.Libelle;
            }

            var dept = ResoudreDepartement(s, byId);

            return new StructureOrganisationnelleDto(
                s.IdStructure,
                s.ParentId,
                s.TypeStructure,
                ExtraireCodeAffichage(s.Code),
                s.Code,
                s.Libelle,
                parentCode,
                parentLibelle,
                dept?.CodeAffichage,
                dept?.Libelle,
                enfantsParParent.GetValueOrDefault(s.IdStructure),
                ubParStructure.GetValueOrDefault(s.IdStructure));
        })
        .OrderBy(s => OrdreType(s.TypeStructure))
        .ThenBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var ubDtos = unites.Select(u => new UniteBudgetaireOrganisationDto(
            u.IdUB,
            u.CodeUB,
            u.Libelle,
            u.Departement.Code,
            u.Departement.Libelle,
            u.FK_StructureOrganisationnelle,
            ExtraireCodeAffichage(u.StructureOrganisationnelle.Code),
            u.StructureOrganisationnelle.Libelle,
            u.StructureOrganisationnelle.TypeStructure)).ToList();

        var compteurs = new ReferentielOrganisationnelCompteursDto(
            structureDtos.Count(s => s.TypeStructure.Equals(TypeStructureOrganisationnelle.Entite, StringComparison.OrdinalIgnoreCase)),
            structureDtos.Count(s => s.TypeStructure.Equals(TypeStructureOrganisationnelle.Departement, StringComparison.OrdinalIgnoreCase)),
            structureDtos.Count,
            ubDtos.Count);

        return new ReferentielOrganisationnelSnapshotDto(compteurs, structureDtos, ubDtos);
    }

    private static StructureRow? ResoudreDepartement(StructureRow node, IReadOnlyDictionary<long, StructureRow> byId)
    {
        var current = node;
        var guard = 0;
        while (guard++ < 20)
        {
            if (current.TypeStructure.Equals(TypeStructureOrganisationnelle.Departement, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            if (current.ParentId is null || !byId.TryGetValue(current.ParentId.Value, out var parent))
            {
                return null;
            }

            current = parent;
        }

        return null;
    }

    public static string ExtraireCodeAffichage(string codeTechnique)
    {
        if (string.IsNullOrWhiteSpace(codeTechnique))
        {
            return codeTechnique;
        }

        var parts = codeTechnique.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? codeTechnique : parts[^1];
    }

    private static int OrdreType(string type)
        => type.ToUpperInvariant() switch
        {
            TypeStructureOrganisationnelle.Entite => 0,
            TypeStructureOrganisationnelle.Departement => 1,
            TypeStructureOrganisationnelle.Direction => 2,
            TypeStructureOrganisationnelle.Division => 3,
            TypeStructureOrganisationnelle.Service => 4,
            TypeStructureOrganisationnelle.Section => 5,
            _ => 6
        };

    private sealed record StructureRow(
        long IdStructure,
        long? ParentId,
        string TypeStructure,
        string Code,
        string Libelle)
    {
        public string CodeAffichage => ExtraireCodeAffichage(Code);
    }
}
