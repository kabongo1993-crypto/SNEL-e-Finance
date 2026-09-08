using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    /// <summary>Charge le contexte complet pour la fiche d'imputation (imputations, snapshots, signataires).</summary>
    public Task<DemandePaiement?> GetFicheImputationContextAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.DemandesPaiement.AsNoTracking()
            .Include(d => d.ExerciceBudgetaire)
            .Include(d => d.TypeBudget)
            .Include(d => d.UtilisateurControle)
            .Include(d => d.UtilisateurVisa)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.TypeBudget)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.UniteBudgetaire)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.RubriqueBudgetaire)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.ItemBI)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.UtilisateurCreation)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.Snapshots)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande, cancellationToken);
}
