using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Invariants workflow DPM — Lot 3.4.</summary>
public class DemandePaiementWorkflowInvariantTests
{
    [Theory]
    [InlineData(StatutDemandePaiement.EnValidationN2, StatutDemandePaiement.EnValidationN1)]
    [InlineData(StatutDemandePaiement.EnControleBudgetaire, StatutDemandePaiement.EnTraitementDpm)]
    [InlineData(StatutDemandePaiement.EnControleBudgetaire, StatutDemandePaiement.EnControleBudgetaire)]
    public void Transitions_Retour_Validees(string from, string to)
        => Assert.True(StatutDemandePaiement.EstTransitionAutorisee(from, to));

    [Theory]
    [InlineData(StatutDemandePaiement.EnValidationN1, StatutDemandePaiement.EnValidationN2)]
    [InlineData(StatutDemandePaiement.EnValidationN1, StatutDemandePaiement.ACorriger)]
    [InlineData(StatutDemandePaiement.EnTraitementDpm, StatutDemandePaiement.ACorriger)]
    public void Transitions_Retour_Demandeur_Validees(string from, string to)
        => Assert.True(StatutDemandePaiement.EstTransitionAutorisee(from, to));

    [Theory]
    [InlineData(StatutDemandePaiement.EnValidationN2, StatutDemandePaiement.ACorriger)]
    [InlineData(StatutDemandePaiement.EnControleBudgetaire, StatutDemandePaiement.ACorriger)]
    [InlineData(StatutDemandePaiement.EnTraitementDpm, StatutDemandePaiement.EnValidationN1)]
    [InlineData(StatutDemandePaiement.ViseeBudgetairement, StatutDemandePaiement.EnControleBudgetaire)]
    [InlineData(StatutDemandePaiement.EnValidationN1, StatutDemandePaiement.Soumise)]
    public void Transitions_Interdites_Ne_Sont_Pas_Ouvertes(string from, string to)
        => Assert.False(StatutDemandePaiement.EstTransitionAutorisee(from, to));

    [Fact]
    public async Task RemettreEnBrouillon_Reinitialise_Assignation_Createur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await y1.RetournerAsync(id, new RetourDemandePaiementRequest("Correction", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.ACorriger, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, tracked.FK_UtilisateurAssigne);
        Assert.NotEmpty(repo.Routages.Where(r => r.FK_DemandePaiement == id && r.EstActif));

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        await x1.RemettreEnBrouillonAsync(id);

        tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.Brouillon, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, tracked.FK_UtilisateurAssigne);
        Assert.DoesNotContain(repo.Routages, r => r.FK_DemandePaiement == id && r.EstActif);
    }

    [Fact]
    public async Task ACorriger_Uniquement_Pour_Retours_Demandeur()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Retour inter-étapes", null));
        Assert.Equal(StatutDemandePaiement.EnTraitementDpm, DemandePaiementRoutageTestHelpers.Tracked(repo, id).Statut);

        repo.Routages.Clear();
        var id2 = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id2);
        await y1.RetournerAsync(id2, new RetourDemandePaiementRequest("Retour demandeur", null));
        Assert.Equal(StatutDemandePaiement.ACorriger, DemandePaiementRoutageTestHelpers.Tracked(repo, id2).Statut);
    }

    [Fact]
    public async Task N2_Rejet_Ne_Produit_Pas_ACorriger()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2 = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        await n2.RejeterValidationEntiteAsync(
            created.IdDemandePaiement,
            new RetourDemandePaiementRequest("Correction", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, tracked.Statut);
        Assert.NotEqual(StatutDemandePaiement.ACorriger, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, tracked.FK_UtilisateurAssigne);
    }

    [Fact]
    public async Task Pool_Soumise_Na_Pas_Assignation()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.Soumise, tracked.Statut);
        Assert.Null(tracked.FK_UtilisateurAssigne);
    }
}
