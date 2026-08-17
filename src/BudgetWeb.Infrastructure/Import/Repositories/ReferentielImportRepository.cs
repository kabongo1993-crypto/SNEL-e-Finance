using System.Security.Cryptography;
using System.Text;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.DTOs;
using BudgetWeb.Application.ReferentielOrganisationnel.Import.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Import.Repositories;

public class ReferentielImportRepository : IReferentielImportRepository
{
    private readonly BudgetDbContext _context;

    public ReferentielImportRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ReferentielOrganisationnelEstVideAsync(CancellationToken cancellationToken = default)
    {
        var departements = await _context.Departements.AnyAsync(cancellationToken);
        var structures = await _context.StructuresOrganisationnelles.AnyAsync(cancellationToken);
        var ub = await _context.UnitesBudgetaires.AnyAsync(cancellationToken);
        return !departements && !structures && !ub;
    }

    public async Task<ImportCompteursDto> ImporterAsync(ImportPreviewDto preview, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var departementsParSigle = new Dictionary<string, Departement>(StringComparer.OrdinalIgnoreCase);
            var structuresParCle = new Dictionary<string, StructureOrganisationnelle>(StringComparer.OrdinalIgnoreCase);
            var codesSqlUtilises = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var entites = preview.Structures
                .Where(s => s.TypeStructure.Equals(TypeStructureOrganisationnelle.Entite, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Code)
                .ToList();

            foreach (var entite in entites)
            {
                var entity = new StructureOrganisationnelle
                {
                    FK_StructureOrganisationnelleParent = null,
                    TypeStructure = TypeStructureOrganisationnelle.Entite,
                    Code = ConstruireCodeSqlUnique(entite.CleMetier, codesSqlUtilises),
                    Libelle = Tronquer(entite.Libelle, 200),
                    Actif = true,
                    DateCreation = now
                };
                _context.StructuresOrganisationnelles.Add(entity);
                structuresParCle[entite.CleMetier] = entity;
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var dept in preview.Departements.OrderBy(d => d.Sigle))
            {
                var entity = new Departement
                {
                    Code = Tronquer(dept.Sigle, 30),
                    Libelle = Tronquer(dept.Libelle, 200),
                    Actif = true,
                    DateCreation = now
                };
                _context.Departements.Add(entity);
                departementsParSigle[dept.Sigle] = entity;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var autresStructures = preview.Structures
                .Where(s => !s.TypeStructure.Equals(TypeStructureOrganisationnelle.Entite, StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => ProfondeurCle(s.CleMetier))
                .OrderBy(g => g.Key);

            foreach (var niveau in autresStructures)
            {
                foreach (var structure in niveau.OrderBy(s => s.CleMetier))
                {
                    long? parentId = null;
                    if (!string.IsNullOrWhiteSpace(structure.CleMetierParent) &&
                        structuresParCle.TryGetValue(structure.CleMetierParent, out var parent))
                    {
                        parentId = parent.IdStructure;
                    }

                    var entity = new StructureOrganisationnelle
                    {
                        FK_StructureOrganisationnelleParent = parentId,
                        TypeStructure = structure.TypeStructure.ToUpperInvariant(),
                        Code = ConstruireCodeSqlUnique(structure.CleMetier, codesSqlUtilises),
                        Libelle = Tronquer(structure.Libelle, 200),
                        Actif = true,
                        DateCreation = now
                    };

                    _context.StructuresOrganisationnelles.Add(entity);
                    structuresParCle[structure.CleMetier] = entity;
                }

                // Save par profondeur pour respecter FK_STRUCTURE_PARENT (self-reference).
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var ub in preview.UnitesBudgetaires.OrderBy(u => u.CodeUB))
            {
                if (!departementsParSigle.TryGetValue(ub.DepartementSigle, out var departement))
                {
                    throw new InvalidOperationException($"Département introuvable pour UB {ub.CodeUB} : {ub.DepartementSigle}");
                }

                if (!structuresParCle.TryGetValue(ub.CleMetierStructure, out var structure))
                {
                    throw new InvalidOperationException($"Structure introuvable pour UB {ub.CodeUB} : {ub.CleMetierStructure}");
                }

                _context.UnitesBudgetaires.Add(new UniteBudgetaire
                {
                    CodeUB = Tronquer(ub.CodeUB, 30),
                    Libelle = Tronquer(ub.Libelle, 200),
                    FK_Departement = departement.IdDepartement,
                    FK_StructureOrganisationnelle = structure.IdStructure,
                    Actif = true,
                    DateCreation = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return preview.Analyse.Compteurs;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// UQ_STRUCTURE_Code impose l'unicité globale de Code.
    /// La clé métier (chemin) est donc transformée en code SQL déterministe ≤ 30 caractères.
    /// Ex. ENTITE|AC → AC ; ENTITE|AC>DEPARTEMENT|DDI → E.AC.DP.DDI ; …>DIVISION|GCC → …DV.GCC
    /// </summary>
    public static string ConstruireCodeSqlUnique(string cleMetier, ISet<string> codesUtilises)
    {
        var candidate = ComposerCodeDepuisCle(cleMetier);
        if (candidate.Length > 30)
        {
            candidate = Hacher(cleMetier, 30);
        }

        if (codesUtilises.Add(candidate))
        {
            return candidate;
        }

        // Collision extrêmement rare après hash : suffixe numérique.
        for (var i = 2; i < 1000; i++)
        {
            var suffix = i.ToString();
            var truncated = candidate[..Math.Min(candidate.Length, 30 - suffix.Length)] + suffix;
            if (codesUtilises.Add(truncated))
            {
                return truncated;
            }
        }

        throw new InvalidOperationException($"Impossible de générer un Code SQL unique pour {cleMetier}.");
    }

    private static string ComposerCodeDepuisCle(string cleMetier)
    {
        var parts = cleMetier.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var alone = parts[0].Split('|', 2);
            return alone.Length == 2 ? alone[1] : alone[0];
        }

        var segments = new List<string>(parts.Length * 2);
        foreach (var part in parts)
        {
            var tokens = part.Split('|', 2);
            var type = tokens[0];
            var code = tokens.Length == 2 ? tokens[1] : tokens[0];
            segments.Add(AbregerType(type));
            segments.Add(code);
        }

        return string.Join('.', segments);
    }

    private static string AbregerType(string typeStructure)
        => typeStructure.ToUpperInvariant() switch
        {
            TypeStructureOrganisationnelle.Entite => "E",
            TypeStructureOrganisationnelle.Departement => "DP",
            TypeStructureOrganisationnelle.Direction => "DI",
            TypeStructureOrganisationnelle.Division => "DV",
            TypeStructureOrganisationnelle.Service => "SV",
            TypeStructureOrganisationnelle.Section => "SC",
            _ => "AU"
        };

    private static string Hacher(string value, int length)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..length];
    }

    private static string Tronquer(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static int ProfondeurCle(string cleMetier)
        => cleMetier.Count(c => c == '>');
}
