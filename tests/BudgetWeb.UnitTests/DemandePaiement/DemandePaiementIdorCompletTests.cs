using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Protection IDOR exhaustive sur mutations DPM — Lot 3.4.</summary>
public class DemandePaiementIdorCompletTests
{
    [Fact]
    public async Task Y2_Ne_Peut_Pas_Receptionner_Dpm_Assignee_A_Y1()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y1);
        await y1.ReceptionnerAsync(id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y2.ReceptionnerAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y2.GetByIdAsync(id));
    }

    [Fact]
    public async Task Y2_Ne_Peut_Pas_Traiter_Ni_Orienter_Dpm_Assignee_A_Y1()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.TraiterChargeAsync(id, new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1)));
    }

    [Fact]
    public async Task Z2_Ne_Peut_Pas_Controle_Ni_Imputer_Dpm_Assignee_A_Z1()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z2 = DemandePaiementRoutageTestHelpers.Junior(repo, 302);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => z2.GetByIdAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => z2.ControlerBudgetaireAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            z2.AddImputationAsync(id, DemandePaiementTestData.ImputationDc()));
    }

    [Fact]
    public async Task N1Autre_Ne_Peut_Pas_Valider_Ni_Rejeter_Dpm_Assignee_A_N1()
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
            new RetourDemandePaiementRequest("Retour", null));

        var id = created.IdDemandePaiement;
        var n1Autre = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1Autre,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => n1Autre.GetByIdAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => n1Autre.ValiderN1ElectroniqueAsync(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            n1Autre.RejeterValidationEntiteAsync(id, new RetourDemandePaiementRequest("Tentative", null)));
    }

    [Fact]
    public async Task Liste_Compteurs_Et_Detail_Coherents_Pour_Y1_Y2()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);

        var listY2 = await y2.ListAsync(new DemandePaiementQuery());
        Assert.DoesNotContain(listY2, d => d.IdDemandePaiement == id);

        var compteursY2 = await y2.GetCompteursAsync(new DemandePaiementCompteursQuery(DemandePaiementListScope.ChargeDpm));
        Assert.Equal(0, compteursY2.EnTraitementDpm);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => y2.GetByIdAsync(id));

        var listY1 = await y1.ListAsync(new DemandePaiementQuery());
        Assert.Contains(listY1, d => d.IdDemandePaiement == id);
        Assert.NotNull(await y1.GetByIdAsync(id));
    }

    [Fact]
    public async Task Demandeur_X2_Ne_Voit_Pas_Dpm_De_X1_En_Liste_Ni_Compteurs()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);
        repo.UbDepartements[20] = 1;

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo, DemandePaiementRoutageTestHelpers.X1);
        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        var x2 = DemandePaiementRoutageTestHelpers.Demandeur(repo, 20);
        var list = await x2.ListAsync(new DemandePaiementQuery(Scope: DemandePaiementListScope.MesDemandes));
        Assert.DoesNotContain(list, d => d.IdDemandePaiement == created.IdDemandePaiement);

        var compteurs = await x2.GetCompteursAsync(
            new DemandePaiementCompteursQuery(DemandePaiementListScope.MesDemandes));
        Assert.Equal(0, compteurs.Brouillon + compteurs.ACorriger);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            x2.GetByIdAsync(created.IdDemandePaiement));
    }
}
