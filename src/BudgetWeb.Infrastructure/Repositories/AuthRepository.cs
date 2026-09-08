using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly BudgetDbContext _db;

    public AuthRepository(BudgetDbContext db)
    {
        _db = db;
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => _db.Utilisateurs.CountAsync(cancellationToken);

    public Task<Utilisateur?> FindByNomUtilisateurAsync(string nomUtilisateur, CancellationToken cancellationToken = default)
    {
        var nom = nomUtilisateur.Trim();
        return _db.Utilisateurs
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NomUtilisateur == nom, cancellationToken);
    }

    public Task<Utilisateur?> FindByIdAsync(long idUtilisateur, CancellationToken cancellationToken = default)
        => _db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.IdUtilisateur == idUtilisateur, cancellationToken);

    public async Task UpdateDerniereConnexionAsync(long idUtilisateur, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.IdUtilisateur == idUtilisateur, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.DateDerniereConnexion = dateUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMotDePasseHashAsync(long idUtilisateur, string motDePasseHash, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.IdUtilisateur == idUtilisateur, cancellationToken)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");

        entity.MotDePasseHash = motDePasseHash;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Utilisateur> CreateAsync(Utilisateur utilisateur, CancellationToken cancellationToken = default)
    {
        _db.Utilisateurs.Add(utilisateur);
        await _db.SaveChangesAsync(cancellationToken);
        return utilisateur;
    }

    public async Task<IReadOnlyList<string>> ListProfilsAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProfilsUtilisateur.AsNoTracking()
            .Where(p => p.FK_Utilisateur == idUtilisateur)
            .Select(p => p.CodeProfil)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListPermissionsIndividuellesAsync(
        long idUtilisateur,
        CancellationToken cancellationToken = default)
    {
        return await _db.PermissionsUtilisateur.AsNoTracking()
            .Where(p => p.FK_Utilisateur == idUtilisateur)
            .Select(p => p.CodePermission)
            .ToListAsync(cancellationToken);
    }
}
