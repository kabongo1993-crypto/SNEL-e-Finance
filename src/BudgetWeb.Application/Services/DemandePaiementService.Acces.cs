using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.Application.Services;

public sealed partial class DemandePaiementService
{
    private async Task<bool> PeutAccederDemandeAsync(
        DemandePaiementEntity demande,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
        => await PeutAccederDemandeAsync(
            ToAccesDemande(demande),
            demande.FK_UniteBudgetaire,
            action,
            cancellationToken);

    private async Task<bool> PeutAccederDemandeAsync(
        DemandePaiementAccesContext contexte,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
        => await PeutAccederDemandeAsync(
            ToAccesDemande(contexte),
            contexte.FK_UniteBudgetaire,
            action,
            cancellationToken);

    private async Task<bool> PeutAccederDemandeAsync(
        DemandePaiementAccesRules.Demande regle,
        long fkUniteBudgetaire,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var permissions = _currentUser.Permissions
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ubAccessible = await EstUbAccessiblePourDemandeAsync(
            userId,
            fkUniteBudgetaire,
            permissions,
            cancellationToken);

        return DemandePaiementAccesRules.PeutAcceder(
            regle,
            new DemandePaiementAccesRules.Utilisateur(userId, permissions),
            action,
            ubAccessible);
    }

    private async Task GarantirAccesDemandeAsync(
        DemandePaiementEntity demande,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
    {
        if (!await PeutAccederDemandeAsync(demande, action, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette demande de paiement.");
        }
    }

    private async Task GarantirAccesDemandeAsync(
        DemandePaiementAccesContext contexte,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
    {
        if (!await PeutAccederDemandeAsync(contexte, action, cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette demande de paiement.");
        }
    }

    private Task GarantirAccesDemandeAsync(
        DemandePaiementEntity demande,
        CancellationToken cancellationToken)
        => GarantirAccesDemandeAsync(demande, DemandePaiementAccesAction.Lire, cancellationToken);

    private Task GarantirAccesDemandeAsync(
        DemandePaiementAccesContext contexte,
        CancellationToken cancellationToken)
        => GarantirAccesDemandeAsync(contexte, DemandePaiementAccesAction.Lire, cancellationToken);

    private async Task<bool> EstUbAccessiblePourDemandeAsync(
        long userId,
        long idUb,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken)
    {
        if (DemandePaiementAccesRules.EstAccesTransversalAdmin(permissions))
            return true;

        if (DemandePaiementAccesRules.EstDemandeurPur(permissions))
        {
            return await _repository.UtilisateurPeutAccederUbAsync(userId, idUb, cancellationToken);
        }

        var snapshot = await _perimetre.GetAsync(userId, cancellationToken);
        if (PerimetreAccess.EstConfigure(snapshot))
        {
            return await PeutAccederUbPerimetreAsync(snapshot!, idUb, cancellationToken);
        }

        if (EstChargeDpm())
        {
            return await _repository.UtilisateurPeutAccederUbAsync(userId, idUb, cancellationToken);
        }

        return await _repository.UtilisateurPeutAccederUbAsync(userId, idUb, cancellationToken);
    }

    private static DemandePaiementAccesRules.Demande ToAccesDemande(DemandePaiementEntity demande)
        => new(
            demande.Statut,
            demande.FK_UtilisateurCreation,
            demande.FK_UtilisateurAssigne,
            demande.TypeBudget?.CodeType ?? demande.TypeBudgetSollicite,
            demande.FK_UtilisateurRetour);

    private static DemandePaiementAccesRules.Demande ToAccesDemande(DemandePaiementScopeRow row)
        => new(
            row.Statut,
            row.FK_UtilisateurCreation ?? 0,
            row.FK_UtilisateurAssigne,
            row.CodeTypeBudget,
            row.FK_UtilisateurRetour);

    private static DemandePaiementAccesRules.Demande ToAccesDemande(DemandePaiementAccesContext contexte)
        => new(
            contexte.Statut,
            contexte.FK_UtilisateurCreation,
            contexte.FK_UtilisateurAssigne,
            contexte.CodeTypeBudget ?? contexte.TypeBudgetSollicite);

    private static DemandePaiementAccesRules.Demande ToAccesDemande(DemandePaiementMutationHeaderReadModel header)
        => new(
            header.Statut,
            header.FK_UtilisateurCreation,
            header.FK_UtilisateurAssigne,
            header.CodeTypeBudget ?? header.TypeBudgetSollicite);

    private async Task GarantirAccesDemandeAsync(
        DemandePaiementMutationHeaderReadModel header,
        DemandePaiementAccesAction action,
        CancellationToken cancellationToken)
    {
        if (!await PeutAccederDemandeAsync(
                ToAccesDemande(header),
                header.FK_UniteBudgetaire,
                action,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Vous n'avez pas accès à cette demande de paiement.");
        }
    }

    private static DemandePaiementAccesAction ResoudreAccesPieceMutation(
        DemandePaiementEntity demande,
        string? codeTypePiece = null)
    {
        var statut = StatutDemandePaiement.Normaliser(demande.Statut);
        var estDocumentSigne = string.Equals(
            codeTypePiece,
            TypePieceJointeDpm.DocumentDpmSigne,
            StringComparison.OrdinalIgnoreCase);

        if (estDocumentSigne
            && statut is StatutDemandePaiement.EnValidationN2
                or StatutDemandePaiement.ValideeEntite
                or StatutDemandePaiement.Brouillon
                or StatutDemandePaiement.ACorriger)
        {
            return DemandePaiementAccesAction.JoindreDocumentSigne;
        }

        if (statut is StatutDemandePaiement.Brouillon or StatutDemandePaiement.ACorriger)
            return DemandePaiementAccesAction.Lire;

        if (statut == StatutDemandePaiement.EnTraitementDpm)
            return DemandePaiementAccesAction.Traiter;

        return DemandePaiementAccesAction.Modifier;
    }
}
