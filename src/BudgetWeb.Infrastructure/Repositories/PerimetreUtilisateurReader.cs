using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Security;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class PerimetreUtilisateurReader : IPerimetreUtilisateurReader
{
    private readonly BudgetDbContext _db;

    public PerimetreUtilisateurReader(BudgetDbContext db)
    {
        _db = db;
    }

    public async Task<PerimetreUtilisateurSnapshot?> GetAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var header = await _db.PerimetresUtilisateur.AsNoTracking()
            .Where(p => p.FK_Utilisateur == idUtilisateur)
            .Select(p => new
            {
                p.IdPerimetreUtilisateur,
                p.TousDepartements,
                p.ToutesUnitesBudgetaires
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
            return null;

        IReadOnlyList<long> depts = header.TousDepartements
            ? Array.Empty<long>()
            : await _db.PerimetresDepartement.AsNoTracking()
                .Where(d => d.FK_PerimetreUtilisateur == header.IdPerimetreUtilisateur)
                .Select(d => d.FK_Departement)
                .ToListAsync(cancellationToken);

        IReadOnlyList<long> ubs = header.ToutesUnitesBudgetaires
            ? Array.Empty<long>()
            : await _db.PerimetresUniteBudgetaire.AsNoTracking()
                .Where(u => u.FK_PerimetreUtilisateur == header.IdPerimetreUtilisateur)
                .Select(u => u.FK_UniteBudgetaire)
                .ToListAsync(cancellationToken);

        return new PerimetreUtilisateurSnapshot(
            header.TousDepartements,
            header.ToutesUnitesBudgetaires,
            depts,
            ubs);
    }

    public Task<long?> GetDepartementUbAsync(
        long idUB,
        CancellationToken cancellationToken = default)
        => _db.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => (long?)u.FK_Departement)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> UtilisateurPeutAccederUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(idUtilisateur, cancellationToken);
        if (snapshot is not null && PerimetreAccess.EstConfigure(snapshot))
        {
            var idDepartement = await GetDepartementUbAsync(idUB, cancellationToken);
            if (idDepartement is null)
                return false;

            return PerimetreAccess.PeutAccederUb(snapshot, idUB, idDepartement.Value);
        }

        return await UtilisateurACreePrevisionSurUbAsync(idUtilisateur, idUB, cancellationToken);
    }

    public async Task<IReadOnlyList<long>> ResoudreIdsUbPerimetreAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(idUtilisateur, cancellationToken);
        if (!PerimetreAccess.EstConfigure(snapshot))
            return [];

        if (snapshot!.ToutesUnitesBudgetaires || snapshot.TousDepartements)
        {
            return await _db.UnitesBudgetaires.AsNoTracking()
                .Where(u => u.Actif)
                .Select(u => u.IdUB)
                .ToListAsync(cancellationToken);
        }

        if (snapshot.IdUnitesBudgetaires.Count > 0)
        {
            var result = new List<long>();
            foreach (var idUb in snapshot.IdUnitesBudgetaires)
            {
                var idDept = await GetDepartementUbAsync(idUb, cancellationToken);
                if (idDept is not null
                    && PerimetreAccess.PeutAccederUb(snapshot, idUb, idDept.Value))
                {
                    result.Add(idUb);
                }
            }

            return result.OrderBy(x => x).ToList();
        }

        if (snapshot.IdDepartements.Count > 0)
        {
            return await _db.UnitesBudgetaires.AsNoTracking()
                .Where(u => u.Actif && snapshot.IdDepartements.Contains(u.FK_Departement))
                .Select(u => u.IdUB)
                .ToListAsync(cancellationToken);
        }

        return [];
    }

    public Task<bool> UtilisateurACreePrevisionSurUbAsync(
        long idUtilisateur,
        long idUB,
        CancellationToken cancellationToken = default)
        => _db.PrevisionsBudgetaires.AsNoTracking()
            .AnyAsync(
                p => p.FK_UtilisateurCreation == idUtilisateur && p.FK_UniteBudgetaire == idUB,
                cancellationToken);

    public async Task<IReadOnlyList<long>> ResoudreIdsUbProxyPrevisionAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        return await _db.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_UtilisateurCreation == idUtilisateur)
            .Select(p => p.FK_UniteBudgetaire)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
    }
}
