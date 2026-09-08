using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<IReadOnlyList<DocumentEtabliListItemDto>> ListDocumentsEtablisAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        var debut = query.DateDebut.ToDateTime(TimeOnly.MinValue);
        var finExcl = query.DateFin.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var type = TypeDocumentEtabli.Normaliser(query.TypeDocument);
        var tous = string.IsNullOrEmpty(type);

        var dpm = ApplyDpmFilters(_context.DemandesPaiement.AsNoTracking(), query);
        var rows = new List<DocumentEtabliListItemDto>();

        if (tous || type == TypeDocumentEtabli.BilletConversion)
        {
            rows.AddRange(await (
                from b in _context.BilletsConversion.AsNoTracking()
                join d in dpm on b.FK_DemandePaiement equals d.IdDemandePaiement
                where b.Statut == StatutBilletConversion.Etabli
                      && b.DateEtabli >= debut
                      && b.DateEtabli < finExcl
                select new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.BilletConversion,
                    "Billet de conversion",
                    b.DemandeChequeNumero ?? string.Empty,
                    b.DateEtabli,
                    b.DateConversion,
                    b.MontantCdf,
                    "CDF",
                    string.Empty,
                    b.UtilisateurEtabli.Prenom + " " + b.UtilisateurEtabli.Nom,
                    "billet-conversion",
                    d.FK_UniteBudgetaire)
            ).ToListAsync(cancellationToken));
        }

        if (tous || type == TypeDocumentEtabli.PieceCaisse)
        {
            rows.AddRange(await (
                from p in _context.PiecesCaisse.AsNoTracking()
                join d in dpm on p.FK_DemandePaiement equals d.IdDemandePaiement
                where p.Statut == StatutDocumentInstrumentPaiement.Etabli
                      && p.DateEtabli >= debut
                      && p.DateEtabli < finExcl
                select new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.PieceCaisse,
                    "Pièce de caisse",
                    p.NumeroPiece,
                    p.DateEtabli,
                    p.DatePiece,
                    p.MontantFc,
                    "CDF",
                    p.BeneficiaireAffichage,
                    p.UtilisateurEtabli.Prenom + " " + p.UtilisateurEtabli.Nom,
                    "piece-caisse",
                    d.FK_UniteBudgetaire)
            ).ToListAsync(cancellationToken));
        }

        if (tous || type == TypeDocumentEtabli.BonProvisoire)
        {
            rows.AddRange(await (
                from b in _context.BonsProvisoire.AsNoTracking()
                join d in dpm on b.FK_DemandePaiement equals d.IdDemandePaiement
                where b.Statut == StatutDocumentInstrumentPaiement.Etabli
                      && b.DateEtabli >= debut
                      && b.DateEtabli < finExcl
                select new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.BonProvisoire,
                    "Bon provisoire",
                    b.NumeroBon,
                    b.DateEtabli,
                    b.DateBon,
                    b.MontantFc,
                    "CDF",
                    b.BeneficiaireAffichage,
                    b.UtilisateurEtabli.Prenom + " " + b.UtilisateurEtabli.Nom,
                    "bon-provisoire",
                    d.FK_UniteBudgetaire)
            ).ToListAsync(cancellationToken));
        }

        if (tous || type == TypeDocumentEtabli.MinuteCheque)
        {
            rows.AddRange(await (
                from m in _context.MinutesCheque.AsNoTracking()
                join d in dpm on m.FK_DemandePaiement equals d.IdDemandePaiement
                where m.Statut == StatutDocumentInstrumentPaiement.Etabli
                      && m.DateEtabli >= debut
                      && m.DateEtabli < finExcl
                select new DocumentEtabliListItemDto(
                    d.IdDemandePaiement,
                    d.Reference,
                    TypeDocumentEtabli.MinuteCheque,
                    "Minute de chèque",
                    m.NumeroOp,
                    m.DateEtabli,
                    m.DateDocument,
                    m.MontantPaiement,
                    m.DevisePaiement,
                    m.BeneficiaireAffichage,
                    m.UtilisateurEtabli.Prenom + " " + m.UtilisateurEtabli.Nom,
                    "minute-cheque",
                    d.FK_UniteBudgetaire)
            ).ToListAsync(cancellationToken));
        }

        return rows
            .OrderByDescending(r => r.DateEtabli)
            .ThenBy(r => r.Reference, StringComparer.Ordinal)
            .ThenBy(r => r.TypeDocument, StringComparer.Ordinal)
            .ToList();
    }

    private static IQueryable<Domain.Entities.DemandePaiement> ApplyDpmFilters(
        IQueryable<Domain.Entities.DemandePaiement> dpm,
        DocumentsEtablisQuery query)
    {
        if (query.IdExercice is long idEx)
            dpm = dpm.Where(d => d.FK_ExerciceBudgetaire == idEx);
        if (query.IdUB is long idUb)
            dpm = dpm.Where(d => d.FK_UniteBudgetaire == idUb);
        if (query.IdDepartement is long idDept)
            dpm = dpm.Where(d => d.UniteBudgetaire.FK_Departement == idDept);
        return dpm;
    }
}
