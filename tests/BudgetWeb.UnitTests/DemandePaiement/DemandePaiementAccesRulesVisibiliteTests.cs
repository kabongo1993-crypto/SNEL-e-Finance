using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Visible ≠ Actionnable — lecture périmètre N1/N2 sans élargir les actions.</summary>
public class DemandePaiementAccesRulesVisibiliteTests
{
    private static DemandePaiementAccesRules.Demande Demande(
        string statut,
        long createur = 10,
        long? assigne = null,
        string? codeType = "DC")
        => new(statut, createur, assigne, codeType);

    private static DemandePaiementAccesRules.Utilisateur User(
        long id,
        params string[] permissions)
        => new(id, permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void N1_Lit_EnValidationN2_SansPouvoirValiderN2()
    {
        var n1 = User(2, AppPermissions.PaiementsLire, AppPermissions.PaiementsValiderN1);
        var d = Demande(StatutDemandePaiement.EnValidationN2, createur: 10, assigne: 3);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            d, n1, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            d, n1, DemandePaiementAccesAction.ValiderN2, ubAccessible: true));
    }

    [Fact]
    public void N2_Lit_EnValidationN1_SansPouvoirValiderN1()
    {
        var n2 = User(3, AppPermissions.PaiementsLire, AppPermissions.PaiementsValiderN2);
        var d = Demande(StatutDemandePaiement.EnValidationN1, createur: 10, assigne: null);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            d, n2, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            d, n2, DemandePaiementAccesAction.ValiderN1, ubAccessible: true));
    }

    [Fact]
    public void DemandeurPur_NeLitQueSesDpm()
    {
        var demandeur = User(
            10,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsEcrire,
            AppPermissions.PaiementsEnvoyerValidation);
        var autre = Demande(StatutDemandePaiement.EnValidationN1, createur: 99, assigne: null);
        var sienne = Demande(StatutDemandePaiement.EnValidationN1, createur: 10, assigne: null);

        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            autre, demandeur, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            sienne, demandeur, DemandePaiementAccesAction.Lire, ubAccessible: true));
    }

    [Fact]
    public void ChargeDpm_Lit_CycleBudget_SansElargirValiderN1()
    {
        var charge = User(
            5,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsChargeDpm);
        var d = Demande(StatutDemandePaiement.EnControleBudgetaire, createur: 10, assigne: 7);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            d, charge, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            d, charge, DemandePaiementAccesAction.ValiderN1, ubAccessible: true));
    }

    [Fact]
    public void ChargeDpm_NeLitPas_ACorriger_DuCircuitEntite()
    {
        var charge = User(
            5,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsChargeDpm,
            AppPermissions.PaiementsReceptionBudget);
        var retourN1 = new DemandePaiementAccesRules.Demande(
            StatutDemandePaiement.ACorriger,
            10,
            10,
            "DC",
            FK_UtilisateurRetour: 2);

        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            retourN1, charge, DemandePaiementAccesAction.Lire, ubAccessible: true));
    }

    [Fact]
    public void ChargeDpm_Lit_ACorriger_QuIlALuiMemeRetourne()
    {
        var charge = User(
            5,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsChargeDpm);
        var retourCharge = new DemandePaiementAccesRules.Demande(
            StatutDemandePaiement.ACorriger,
            10,
            10,
            "DC",
            FK_UtilisateurRetour: 5);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            retourCharge, charge, DemandePaiementAccesAction.Lire, ubAccessible: true));
    }

    [Fact]
    public void N1_LitToujours_ACorriger_DuCircuitEntite()
    {
        var n1 = User(2, AppPermissions.PaiementsLire, AppPermissions.PaiementsValiderN1);
        var retourN1 = new DemandePaiementAccesRules.Demande(
            StatutDemandePaiement.ACorriger,
            10,
            10,
            "DC",
            FK_UtilisateurRetour: 2);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            retourN1, n1, DemandePaiementAccesAction.Lire, ubAccessible: true));
    }

    [Fact]
    public void N1_Lit_StatutsCycle_SansElargirLesActions()
    {
        var n1 = User(2, AppPermissions.PaiementsLire, AppPermissions.PaiementsValiderN1);
        var soumise = Demande(StatutDemandePaiement.Soumise, createur: 10, assigne: 5);
        var traitement = Demande(StatutDemandePaiement.EnTraitementDpm, createur: 10, assigne: 5);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            soumise, n1, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            traitement, n1, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            soumise, n1, DemandePaiementAccesAction.Receptionner, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            traitement, n1, DemandePaiementAccesAction.Traiter, ubAccessible: true));
    }

    [Fact]
    public void N1_NeLitPas_Brouillon_DAutrui()
    {
        var n1 = User(2, AppPermissions.PaiementsLire, AppPermissions.PaiementsValiderN1);
        var brouillon = Demande(StatutDemandePaiement.Brouillon, createur: 10, assigne: 10);

        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            brouillon, n1, DemandePaiementAccesAction.Lire, ubAccessible: true));
    }

    [Fact]
    public void ChargeDpm_Lit_ValidationEntite_SansActionN1()
    {
        var charge = User(
            5,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsChargeDpm);
        var n1 = Demande(StatutDemandePaiement.EnValidationN1, createur: 10, assigne: null);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            n1, charge, DemandePaiementAccesAction.Lire, ubAccessible: true));
        Assert.False(DemandePaiementAccesRules.PeutAcceder(
            n1, charge, DemandePaiementAccesAction.ValiderN1, ubAccessible: true));
    }

    [Fact]
    public void Createur_Peut_Remplacer_Document_Signe_Assigne_A_N2()
    {
        var createur = User(
            10,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsJoindreDocumentSigne);
        var d = Demande(StatutDemandePaiement.EnValidationN2, createur: 10, assigne: 202);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            d, createur, DemandePaiementAccesAction.JoindreDocumentSigne, ubAccessible: true));
    }

    [Fact]
    public void Createur_Peut_Remplacer_Document_Signe_ValideeEntite()
    {
        var createur = User(
            10,
            AppPermissions.PaiementsLire,
            AppPermissions.PaiementsJoindreDocumentSigne);
        var d = Demande(StatutDemandePaiement.ValideeEntite, createur: 10, assigne: 202);

        Assert.True(DemandePaiementAccesRules.PeutAcceder(
            d, createur, DemandePaiementAccesAction.JoindreDocumentSigne, ubAccessible: true));
    }
}
