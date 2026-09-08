using BudgetWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task DeleteDemandeGraphAsync(
        DemandePaiement demande,
        CancellationToken cancellationToken = default)
    {
        var idDemande = demande.IdDemandePaiement;

        var audits = await _context.JournalAudits
            .Where(j => j.Entite == "DEMANDE_PAIEMENT" && j.IdEntite == idDemande)
            .ToListAsync(cancellationToken);
        if (audits.Count > 0)
            _context.JournalAudits.RemoveRange(audits);

        if (demande.ValidationsEntite.Count > 0)
            _context.DemandePaiementValidations.RemoveRange(demande.ValidationsEntite);

        var imputationIds = demande.Imputations.Select(i => i.IdImputation).ToList();
        if (imputationIds.Count > 0)
        {
            var snapshots = await _context.DemandePaiementImputationSnapshots
                .Where(s => imputationIds.Contains(s.FK_Imputation) || s.FK_DemandePaiement == idDemande)
                .ToListAsync(cancellationToken);
            if (snapshots.Count > 0)
                _context.DemandePaiementImputationSnapshots.RemoveRange(snapshots);
        }

        if (demande.Imputations.Count > 0)
            _context.DemandePaiementImputations.RemoveRange(demande.Imputations);

        if (demande.PiecesJointes.Count > 0)
            _context.PiecesJointes.RemoveRange(demande.PiecesJointes);

        if (demande.Beneficiaires.Count > 0)
            _context.DemandePaiementBeneficiaires.RemoveRange(demande.Beneficiaires);

        if (demande.BilletConversion is not null)
            _context.BilletsConversion.Remove(demande.BilletConversion);
        if (demande.PieceCaisse is not null)
            _context.PiecesCaisse.Remove(demande.PieceCaisse);
        if (demande.BonProvisoire is not null)
            _context.BonsProvisoire.Remove(demande.BonProvisoire);
        if (demande.MinuteCheque is not null)
            _context.MinutesCheque.Remove(demande.MinuteCheque);

        _context.DemandesPaiement.Remove(demande);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
