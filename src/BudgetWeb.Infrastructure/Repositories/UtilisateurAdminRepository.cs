using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class UtilisateurAdminRepository : IUtilisateurAdminRepository
{
    private readonly BudgetDbContext _db;

    public UtilisateurAdminRepository(BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UtilisateurAdminListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.Utilisateurs.AsNoTracking()
            .Include(u => u.DepartementPrincipal)
            .Include(u => u.Profils)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UtilisateurAdminListItemDto(
            u.IdUtilisateur,
            u.Matricule,
            u.Nom,
            u.Postnom,
            u.Prenom,
            BuildNomComplet(u),
            u.NomUtilisateur,
            u.Email,
            u.Actif,
            u.DateDerniereConnexion,
            u.FK_DepartementPrincipal,
            u.DepartementPrincipal?.Code,
            u.DepartementPrincipal?.Libelle,
            u.Profils.Select(p => p.CodeProfil).OrderBy(c => c).ToList())).ToList();
    }

    public async Task<UtilisateurAdminDetailDto?> GetDetailAsync(long idUtilisateur, CancellationToken cancellationToken = default)
    {
        var u = await _db.Utilisateurs.AsNoTracking()
            .Include(x => x.DepartementPrincipal)
            .Include(x => x.StructureOrganisationnelle)
            .Include(x => x.StructureService)
            .Include(x => x.Profils)
            .Include(x => x.PermissionsIndividuelles)
            .Include(x => x.Perimetre!).ThenInclude(p => p.Departements)
            .Include(x => x.Perimetre!).ThenInclude(p => p.UnitesBudgetaires)
            .FirstOrDefaultAsync(x => x.IdUtilisateur == idUtilisateur, cancellationToken);

        if (u is null)
            return null;

        return MapDetail(u);
    }

    public Task<bool> ExistsNomUtilisateurAsync(string nomUtilisateur, long? excludeId, CancellationToken cancellationToken = default)
    {
        var nom = nomUtilisateur.Trim();
        return _db.Utilisateurs.AsNoTracking()
            .AnyAsync(u => u.NomUtilisateur == nom && (excludeId == null || u.IdUtilisateur != excludeId), cancellationToken);
    }

    public Task<bool> ExistsMatriculeAsync(string matricule, long? excludeId, CancellationToken cancellationToken = default)
    {
        var m = matricule.Trim();
        return _db.Utilisateurs.AsNoTracking()
            .AnyAsync(u => u.Matricule == m && (excludeId == null || u.IdUtilisateur != excludeId), cancellationToken);
    }

    public async Task<long> CreateAsync(
        Utilisateur utilisateur,
        IReadOnlyList<string> profils,
        IReadOnlyList<string> permissionsIndividuelles,
        PerimetreUtilisateurDto perimetre,
        CancellationToken cancellationToken = default)
    {
        foreach (var code in profils)
        {
            utilisateur.Profils.Add(new ProfilUtilisateur { CodeProfil = code });
        }

        var now = DateTime.UtcNow;
        foreach (var code in permissionsIndividuelles)
        {
            utilisateur.PermissionsIndividuelles.Add(new PermissionUtilisateur
            {
                CodePermission = code,
                DateAttribution = now
            });
        }

        utilisateur.Perimetre = BuildPerimetreEntity(perimetre, now);
        _db.Utilisateurs.Add(utilisateur);
        await _db.SaveChangesAsync(cancellationToken);
        return utilisateur.IdUtilisateur;
    }

    public async Task<bool> UpdateAsync(
        long idUtilisateur,
        Action<Utilisateur> applyIdentity,
        IReadOnlyList<string> profils,
        IReadOnlyList<string> permissionsIndividuelles,
        PerimetreUtilisateurDto perimetre,
        CancellationToken cancellationToken = default)
    {
        var u = await _db.Utilisateurs
            .Include(x => x.Profils)
            .Include(x => x.PermissionsIndividuelles)
            .Include(x => x.Perimetre!).ThenInclude(p => p.Departements)
            .Include(x => x.Perimetre!).ThenInclude(p => p.UnitesBudgetaires)
            .FirstOrDefaultAsync(x => x.IdUtilisateur == idUtilisateur, cancellationToken);

        if (u is null)
            return false;

        applyIdentity(u);

        _db.ProfilsUtilisateur.RemoveRange(u.Profils);
        u.Profils.Clear();
        foreach (var code in profils)
            u.Profils.Add(new ProfilUtilisateur { FK_Utilisateur = idUtilisateur, CodeProfil = code });

        _db.PermissionsUtilisateur.RemoveRange(u.PermissionsIndividuelles);
        u.PermissionsIndividuelles.Clear();
        var now = DateTime.UtcNow;
        foreach (var code in permissionsIndividuelles)
        {
            u.PermissionsIndividuelles.Add(new PermissionUtilisateur
            {
                FK_Utilisateur = idUtilisateur,
                CodePermission = code,
                DateAttribution = now
            });
        }

        if (u.Perimetre is null)
        {
            u.Perimetre = BuildPerimetreEntity(perimetre, now);
            u.Perimetre.FK_Utilisateur = idUtilisateur;
        }
        else
        {
            _db.PerimetresDepartement.RemoveRange(u.Perimetre.Departements);
            _db.PerimetresUniteBudgetaire.RemoveRange(u.Perimetre.UnitesBudgetaires);
            u.Perimetre.Departements.Clear();
            u.Perimetre.UnitesBudgetaires.Clear();
            ApplyPerimetre(u.Perimetre, perimetre, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetActifAsync(long idUtilisateur, bool actif, CancellationToken cancellationToken = default)
    {
        var u = await _db.Utilisateurs.FirstOrDefaultAsync(x => x.IdUtilisateur == idUtilisateur, cancellationToken);
        if (u is null)
            return false;
        u.Actif = actif;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateMotDePasseHashAsync(long idUtilisateur, string hash, CancellationToken cancellationToken = default)
    {
        var u = await _db.Utilisateurs.FirstOrDefaultAsync(x => x.IdUtilisateur == idUtilisateur, cancellationToken);
        if (u is null)
            return false;
        u.MotDePasseHash = hash;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DepartementExistsAsync(long idDepartement, CancellationToken cancellationToken = default)
        => _db.Departements.AsNoTracking().AnyAsync(d => d.IdDepartement == idDepartement, cancellationToken);

    public Task<bool> StructureExistsAsync(long idStructure, CancellationToken cancellationToken = default)
        => _db.StructuresOrganisationnelles.AsNoTracking().AnyAsync(s => s.IdStructure == idStructure, cancellationToken);

    public Task<bool> UbExistsAsync(long idUb, CancellationToken cancellationToken = default)
        => _db.UnitesBudgetaires.AsNoTracking().AnyAsync(u => u.IdUB == idUb, cancellationToken);

    public async Task<(long IdDepartement, bool Actif)?> GetUbDepartementAsync(long idUb, CancellationToken cancellationToken = default)
    {
        var row = await _db.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUb)
            .Select(u => new { u.FK_Departement, u.Actif })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.FK_Departement, row.Actif);
    }

    internal static UtilisateurAdminDetailDto MapDetail(Utilisateur u)
    {
        var profils = u.Profils.Select(p => p.CodeProfil).OrderBy(c => c).ToList();
        var fromProfils = EffectivePermissions.FromProfils(profils);
        var individuelles = u.PermissionsIndividuelles.Select(p => p.CodePermission).OrderBy(c => c).ToList();
        var effectives = EffectivePermissions.Merge(fromProfils, individuelles);
        var etats = AppPermissions.PermissionsComplementaires
            .Select(code =>
            {
                var heritee = fromProfils.Contains(code, StringComparer.OrdinalIgnoreCase);
                var indiv = individuelles.Contains(code, StringComparer.OrdinalIgnoreCase);
                return new PermissionEtatDto(
                    code,
                    AppPermissions.DescriptionPermission(code),
                    heritee,
                    indiv,
                    heritee || indiv);
            })
            .ToList();

        var perimetre = u.Perimetre is null
            ? new PerimetreUtilisateurDto(false, false, [], [])
            : new PerimetreUtilisateurDto(
                u.Perimetre.TousDepartements,
                u.Perimetre.ToutesUnitesBudgetaires,
                u.Perimetre.Departements.Select(d => d.FK_Departement).OrderBy(x => x).ToList(),
                u.Perimetre.UnitesBudgetaires.Select(x => x.FK_UniteBudgetaire).OrderBy(x => x).ToList());

        return new UtilisateurAdminDetailDto(
            u.IdUtilisateur,
            u.Matricule,
            u.Nom,
            u.Postnom,
            u.Prenom,
            u.NomUtilisateur,
            u.Email,
            u.Actif,
            u.DateCreation,
            u.DateDerniereConnexion,
            u.FK_StructureOrganisationnelle,
            u.StructureOrganisationnelle is null
                ? null
                : $"{u.StructureOrganisationnelle.Code} — {u.StructureOrganisationnelle.Libelle}",
            u.FK_DepartementPrincipal,
            u.DepartementPrincipal?.Code,
            u.DepartementPrincipal?.Libelle,
            u.FK_StructureService,
            u.StructureService is null
                ? null
                : $"{u.StructureService.Code} — {u.StructureService.Libelle}",
            profils,
            fromProfils,
            individuelles,
            effectives,
            etats,
            perimetre);
    }

    private static PerimetreUtilisateur BuildPerimetreEntity(PerimetreUtilisateurDto dto, DateTime now)
    {
        var entity = new PerimetreUtilisateur();
        ApplyPerimetre(entity, dto, now);
        return entity;
    }

    private static void ApplyPerimetre(PerimetreUtilisateur entity, PerimetreUtilisateurDto dto, DateTime now)
    {
        entity.TousDepartements = dto.TousDepartements;
        entity.ToutesUnitesBudgetaires = dto.ToutesUnitesBudgetaires;
        entity.DateModification = now;

        if (!dto.TousDepartements)
        {
            foreach (var id in dto.IdDepartements.Distinct())
                entity.Departements.Add(new PerimetreDepartement { FK_Departement = id });
        }

        if (!dto.ToutesUnitesBudgetaires)
        {
            foreach (var id in dto.IdUnitesBudgetaires.Distinct())
                entity.UnitesBudgetaires.Add(new PerimetreUniteBudgetaire { FK_UniteBudgetaire = id });
        }
    }

    private static string BuildNomComplet(Utilisateur u)
    {
        var parts = new[] { u.Nom, u.Postnom, u.Prenom }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(' ', parts);
    }
}
