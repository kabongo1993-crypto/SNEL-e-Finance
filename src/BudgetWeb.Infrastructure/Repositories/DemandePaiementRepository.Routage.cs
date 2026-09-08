using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task DesactiverRoutagesActifsAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        await _context.DemandePaiementRoutages
            .Where(r => r.FK_DemandePaiement == idDemande && r.EstActif)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.EstActif, false),
                cancellationToken);
    }

    public Task AddRoutageAsync(
        DemandePaiementRoutage routage,
        CancellationToken cancellationToken = default)
    {
        _context.DemandePaiementRoutages.Add(routage);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => await _context.DemandePaiementRoutages
            .AsNoTracking()
            .Where(r => r.FK_DemandePaiement == idDemande)
            .OrderByDescending(r => r.DateRoutage)
            .ThenByDescending(r => r.IdRoutage)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DemandePaiementRoutage>> GetRoutagesLectureAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.DemandePaiementRoutages
            .AsNoTracking()
            .Where(r => r.FK_DemandePaiement == idDemande)
            .OrderBy(r => r.DateRoutage)
            .ThenBy(r => r.IdRoutage)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return rows;

        var userIds = rows
            .SelectMany(r => new[] { r.FK_UtilisateurSource, r.FK_UtilisateurCible })
            .Distinct()
            .ToList();

        var users = await _context.Utilisateurs
            .AsNoTracking()
            .Where(u => userIds.Contains(u.IdUtilisateur))
            .Select(u => new { u.IdUtilisateur, u.Nom, u.Prenom })
            .ToListAsync(cancellationToken);

        var userMap = users.ToDictionary(u => u.IdUtilisateur);

        foreach (var row in rows)
        {
            if (userMap.TryGetValue(row.FK_UtilisateurSource, out var source))
            {
                row.UtilisateurSource = new Utilisateur
                {
                    IdUtilisateur = source.IdUtilisateur,
                    Nom = source.Nom,
                    Prenom = source.Prenom,
                };
            }

            if (row.FK_UtilisateurCible > 0
                && userMap.TryGetValue(row.FK_UtilisateurCible, out var cible))
            {
                row.UtilisateurCible = new Utilisateur
                {
                    IdUtilisateur = cible.IdUtilisateur,
                    Nom = cible.Nom,
                    Prenom = cible.Prenom,
                };
            }
        }

        return rows;
    }
}
