using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public Task<DemandePaiement?> GetForPieceUploadAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.DemandesPaiement
            .FirstOrDefaultAsync(d => d.IdDemandePaiement == idDemande, cancellationToken);

    public Task<bool> HasValidatedEntiteValidationsAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => _context.DemandePaiementValidations.AsNoTracking()
            .AnyAsync(
                v => v.FK_DemandePaiement == idDemande
                     && v.Statut == StatutValidationEntite.Validee,
                cancellationToken);

    public async Task AttachEmpreinteCollectionsForUploadAsync(
        DemandePaiement demande,
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        demande.ValidationsEntite = await _context.DemandePaiementValidations
            .Where(v => v.FK_DemandePaiement == idDemande)
            .OrderBy(v => v.Ordre)
            .ToListAsync(cancellationToken);

        demande.Beneficiaires = await _context.DemandePaiementBeneficiaires
            .Where(b => b.FK_DemandePaiement == idDemande)
            .OrderBy(b => b.Ordre)
            .ToListAsync(cancellationToken);

        demande.PiecesJointes = await _context.PiecesJointes
            .Where(p => p.FK_DemandePaiement == idDemande)
            .OrderBy(p => p.DateUpload)
            .ToListAsync(cancellationToken);

        demande.Imputations = await _context.DemandePaiementImputations
            .Where(i => i.FK_DemandePaiement == idDemande)
            .OrderBy(i => i.Ordre)
            .ToListAsync(cancellationToken);
    }
}
