using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Règles de backfill FK_UtilisateurAssigne (Lot 3.1) — miroir du script SQL.
/// </summary>
public class DemandePaiementAssignationBackfillTests
{
    private const long Createur = 10;
    private const long Receptionneur = 20;
    private const long Controleur = 30;

    [Theory]
    [InlineData(StatutDemandePaiement.Brouillon)]
    [InlineData(StatutDemandePaiement.ACorriger)]
    public void Brouillon_Et_ACorriger_Assignent_Createur(string statut)
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignation(
            statut,
            Createur,
            fkUtilisateurReception: null);

        Assert.Equal(Createur, assigne);
    }

    [Theory]
    [InlineData(StatutDemandePaiement.EnValidationN1)]
    [InlineData(StatutDemandePaiement.EnValidationN2)]
    [InlineData(StatutDemandePaiement.ValideeEntite)]
    [InlineData(StatutDemandePaiement.Soumise)]
    [InlineData(StatutDemandePaiement.ViseeBudgetairement)]
    public void Statuts_Pool_Retournent_Null(string statut)
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignation(
            statut,
            Createur,
            fkUtilisateurReception: Receptionneur);

        Assert.Null(assigne);
    }

    [Fact]
    public void EnTraitement_Avec_Receptionneur_Assigne_Receptionneur()
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignation(
            StatutDemandePaiement.EnTraitementDpm,
            Createur,
            Receptionneur);

        Assert.Equal(Receptionneur, assigne);
    }

    [Fact]
    public void EnTraitement_Sans_Receptionneur_Retourne_Null()
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignation(
            StatutDemandePaiement.EnTraitementDpm,
            Createur,
            fkUtilisateurReception: null);

        Assert.Null(assigne);
    }

    [Fact]
    public void ControleBudgetaire_Audit_Controler_Prioritaire()
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignationControleBudgetaire(
            fkUtilisateurControle: 99,
            new DemandePaiementAssignationBackfillRules.ControleAuditContext(
                DernierControleurAudit: Controleur,
                PossedeAuditOrienter: true));

        Assert.Equal(Controleur, assigne);
    }

    [Fact]
    public void ControleBudgetaire_Sans_Audit_Orienter_Fallback_Controle()
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignationControleBudgetaire(
            fkUtilisateurControle: Controleur,
            new DemandePaiementAssignationBackfillRules.ControleAuditContext(
                DernierControleurAudit: null,
                PossedeAuditOrienter: false));

        Assert.Equal(Controleur, assigne);
    }

    [Fact]
    public void ControleBudgetaire_Avec_Orienter_Sans_Controler_Retourne_Null()
    {
        var assigne = DemandePaiementAssignationBackfillRules.ResoudreAssignationControleBudgetaire(
            fkUtilisateurControle: 99,
            new DemandePaiementAssignationBackfillRules.ControleAuditContext(
                DernierControleurAudit: null,
                PossedeAuditOrienter: true));

        Assert.Null(assigne);
    }

    [Fact]
    public void Backfill_Ne_Modifie_Pas_Les_Statuts_Metier()
    {
        // Les règles de backfill ne produisent que FK_UtilisateurAssigne — jamais de statut.
        var statuts = StatutDemandePaiement.ValeursAutorisees;
        Assert.All(statuts, statut =>
        {
            _ = DemandePaiementAssignationBackfillRules.ResoudreAssignation(
                statut,
                Createur,
                Receptionneur);
        });
    }
}
