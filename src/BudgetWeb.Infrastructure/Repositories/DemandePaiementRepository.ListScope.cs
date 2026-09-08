using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed partial class DemandePaiementRepository
{
    public async Task<IReadOnlyList<DemandePaiementScopeRow>> ListScopeRowsAsync(
        DemandePaiementQuery query,
        CancellationToken cancellationToken = default)
    {
        var q = ApplyListQueryFilters(_context.DemandesPaiement.AsNoTracking(), query);

        var headers = await q
            .OrderByDescending(d => d.DateCreation)
            .ThenByDescending(d => d.IdDemandePaiement)
            .Select(d => new
            {
                d.IdDemandePaiement,
                d.Statut,
                d.FK_UniteBudgetaire,
                d.FK_UtilisateurCreation,
                d.FK_UtilisateurAssigne,
                d.FK_UtilisateurRetour,
                d.FK_TypeBudget,
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
            return Array.Empty<DemandePaiementScopeRow>();

        var idTypes = headers.Where(h => h.FK_TypeBudget is not null)
            .Select(h => h.FK_TypeBudget!.Value)
            .Distinct()
            .ToList();

        var types = idTypes.Count == 0
            ? []
            : await _context.TypesBudget.AsNoTracking()
                .Where(t => idTypes.Contains(t.IdTypeBudget))
                .Select(t => new { t.IdTypeBudget, t.CodeType })
                .ToListAsync(cancellationToken);
        var typeMap = types.ToDictionary(t => t.IdTypeBudget);

        return headers.Select(h =>
        {
            string? codeType = null;
            if (h.FK_TypeBudget is long idType && typeMap.TryGetValue(idType, out var typeRow))
                codeType = typeRow.CodeType;

            return new DemandePaiementScopeRow(
                h.IdDemandePaiement,
                h.Statut,
                h.FK_UniteBudgetaire,
                h.FK_UtilisateurCreation,
                h.FK_TypeBudget,
                codeType,
                h.FK_UtilisateurAssigne,
                h.FK_UtilisateurRetour);
        }).ToList();
    }

    private static IQueryable<Domain.Entities.DemandePaiement> ApplyListQueryFilters(
        IQueryable<Domain.Entities.DemandePaiement> q,
        DemandePaiementQuery query)
    {
        if (query.IdExercice is long idEx)
            q = q.Where(d => d.FK_ExerciceBudgetaire == idEx);

        if (query.IdUB is long idUb)
            q = q.Where(d => d.FK_UniteBudgetaire == idUb);

        if (query.IdDepartement is long idDept)
            q = q.Where(d => d.UniteBudgetaire.FK_Departement == idDept);

        if (query.IdCasDossier is long idCas)
            q = q.Where(d => d.FK_CasDossier == idCas);

        if (query.IdTypeBudget is long idType)
            q = q.Where(d => d.FK_TypeBudget == idType);

        if (query.IdDemandeur is long idDem)
            q = q.Where(d => d.FK_Demandeur == idDem);

        if (!string.IsNullOrWhiteSpace(query.Statut))
        {
            var st = StatutDemandePaiement.Normaliser(query.Statut);
            if (st == StatutDemandePaiement.EnTraitementDpm)
            {
                q = q.Where(d =>
                    d.Statut == StatutDemandePaiement.EnTraitementDpm
                    || d.Statut == StatutDemandePaiement.ReceptionneeBudgets);
            }
            else
            {
                q = q.Where(d => d.Statut == st);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Reference))
        {
            var reference = query.Reference.Trim();
            q = q.Where(d => d.Reference.Contains(reference));
        }

        if (!string.IsNullOrWhiteSpace(query.Beneficiaire))
        {
            var benef = query.Beneficiaire.Trim().ToLower();
            q = q.Where(d => d.Beneficiaires.Any(b =>
                b.NomComplet.ToLower().Contains(benef)
                || (b.RaisonSociale != null && b.RaisonSociale.ToLower().Contains(benef))
                || (b.Matricule != null && b.Matricule.ToLower().Contains(benef))));
        }

        if (query.DateDebut is DateOnly debut)
            q = q.Where(d => d.DateEmission >= debut);

        if (query.DateFin is DateOnly fin)
            q = q.Where(d => d.DateEmission <= fin);

        return q;
    }
}
