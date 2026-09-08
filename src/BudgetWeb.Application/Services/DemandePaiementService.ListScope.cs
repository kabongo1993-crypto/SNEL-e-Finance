using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    public async Task<DemandePaiementCompteursDto> GetCompteursAsync(
        DemandePaiementCompteursQuery query,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();

        var listQuery = query.ToListQuery();
        if (listQuery.IdUB is long idUb && !PeutVoirToutesUb())
            await GarantirAccesUbAsync(idUb, cancellationToken);

        var scopeRows = await _repository.ListScopeRowsAsync(listQuery, cancellationToken);
        var entities = scopeRows.Select(ToScopeEntity).ToList();
        var scoped = await ApplyListScopeAsync(entities, listQuery, query.Scope, cancellationToken);
        return AggregateCompteurs(scoped);
    }

    /// <summary>
    /// Périmètre métier partagé entre <see cref="ListAsync"/> et <see cref="GetCompteursAsync"/>.
    /// </summary>
    private async Task<IReadOnlyList<DemandePaiementEntity>> ApplyListScopeAsync(
        IReadOnlyList<DemandePaiementEntity> rows,
        DemandePaiementQuery query,
        string? listScope,
        CancellationToken cancellationToken)
    {
        if (ShouldApplyChargeDpmFileFilter(query, listScope))
        {
            rows = rows.Where(d =>
            {
                var st = StatutDemandePaiement.Normaliser(d.Statut);
                return st is StatutDemandePaiement.Soumise
                    or StatutDemandePaiement.EnTraitementDpm
                    or StatutDemandePaiement.EnControleBudgetaire
                    or StatutDemandePaiement.ACorriger;
            }).ToList();
        }

        var userId = _currentUser.RequireUserId();
        var permissions = _currentUser.Permissions
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var utilisateur = new DemandePaiementAccesRules.Utilisateur(userId, permissions);
        var filtered = new List<DemandePaiementEntity>();

        foreach (var d in rows)
        {
            var ubAccessible = await EstUbAccessiblePourDemandeAsync(
                userId,
                d.FK_UniteBudgetaire,
                permissions,
                cancellationToken);

            if (DemandePaiementAccesRules.PeutAcceder(
                    ToAccesDemande(d),
                    utilisateur,
                    DemandePaiementAccesAction.Lire,
                    ubAccessible))
            {
                filtered.Add(d);
            }
        }

        rows = filtered;

        var filiereScope = ResolveFiliereFromScope(listScope);
        if (filiereScope is not null)
        {
            rows = rows.Where(d =>
                string.Equals(d.TypeBudget?.CodeType, filiereScope, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (!DemandePaiementAccesRules.EstAccesTransversalAdmin(permissions))
        {
            var filiere = FiliereJuniorExclusive();
            if (filiere is not null)
            {
                rows = rows.Where(d =>
                    string.Equals(d.TypeBudget?.CodeType, filiere, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        return rows;
    }

    private bool ShouldApplyChargeDpmFileFilter(DemandePaiementQuery query, string? listScope)
    {
        // Vue charge-dpm : navigation par statut sur tout le parcours DPM (règles d'accès inchangées).
        if (string.Equals(listScope, DemandePaiementListScope.ChargeDpm, StringComparison.OrdinalIgnoreCase))
            return false;

        // Page « mes demandes » : compteurs et navigation par statut couvrent tout le parcours
        // (y compris EN_CONTROLE_BUDGETAIRE). Le filtre file active est réservé aux listes legacy.
        if (string.Equals(listScope, DemandePaiementListScope.MesDemandes, StringComparison.OrdinalIgnoreCase))
            return false;

        return EstChargeDpm()
            && !_currentUser.HasPermission(AppPermissions.AdminAll)
            && string.IsNullOrWhiteSpace(query.Statut);
    }

    private static string? ResolveFiliereFromScope(string? scope)
        => scope switch
        {
            DemandePaiementListScope.JuniorDc => TypeBudgetCode.DepensesCourantes,
            DemandePaiementListScope.JuniorAe => TypeBudgetCode.ActionsExploitation,
            DemandePaiementListScope.JuniorBi => TypeBudgetCode.BudgetInvestissement,
            _ => null,
        };

    private static DemandePaiementEntity ToScopeEntity(DemandePaiementScopeRow row)
        => new()
        {
            IdDemandePaiement = row.IdDemandePaiement,
            Statut = row.Statut,
            FK_UniteBudgetaire = row.FK_UniteBudgetaire,
            FK_UtilisateurCreation = row.FK_UtilisateurCreation ?? 0,
            FK_UtilisateurAssigne = row.FK_UtilisateurAssigne,
            FK_UtilisateurRetour = row.FK_UtilisateurRetour,
            FK_TypeBudget = row.FK_TypeBudget,
            TypeBudget = row.CodeTypeBudget is not null
                ? new Domain.Entities.TypeBudget { CodeType = row.CodeTypeBudget }
                : null,
        };

    private static DemandePaiementCompteursDto AggregateCompteurs(IReadOnlyList<DemandePaiementEntity> rows)
    {
        var buckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var st = StatutDemandePaiement.Normaliser(row.Statut);
            buckets.TryGetValue(st, out var count);
            buckets[st] = count + 1;
        }

        return new DemandePaiementCompteursDto(
            Total: rows.Count,
            Brouillon: buckets.GetValueOrDefault(StatutDemandePaiement.Brouillon),
            EnValidationN1: buckets.GetValueOrDefault(StatutDemandePaiement.EnValidationN1),
            EnValidationN2: buckets.GetValueOrDefault(StatutDemandePaiement.EnValidationN2),
            ValideeEntite: buckets.GetValueOrDefault(StatutDemandePaiement.ValideeEntite),
            Soumise: buckets.GetValueOrDefault(StatutDemandePaiement.Soumise),
            EnTraitementDpm: buckets.GetValueOrDefault(StatutDemandePaiement.EnTraitementDpm),
            EnControleBudgetaire: buckets.GetValueOrDefault(StatutDemandePaiement.EnControleBudgetaire),
            ACorriger: buckets.GetValueOrDefault(StatutDemandePaiement.ACorriger),
            ViseeBudgetairement: buckets.GetValueOrDefault(StatutDemandePaiement.ViseeBudgetairement));
    }
}
