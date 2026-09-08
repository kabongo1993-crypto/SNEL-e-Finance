using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Destination budgétaire sollicitée et mode paiement à l'initialisation DPM.</summary>
public class DestinationBudgetaireSolliciteeTests
{
    [Fact]
    public async Task Creation_Dc_Sans_Item()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                typeBudgetSollicite: TypeBudgetCode.DepensesCourantes,
                itemSollicite: null));

        Assert.Equal(TypeBudgetCode.DepensesCourantes, created.TypeBudgetSollicite);
        Assert.Null(created.ItemSollicite);
        Assert.Empty(repo.Demandes.Single().Imputations);
    }

    [Fact]
    public async Task Creation_Ae_Avec_Item_Obligatoire()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                typeBudgetSollicite: TypeBudgetCode.ActionsExploitation,
                itemSollicite: "025"));

        Assert.Equal(TypeBudgetCode.ActionsExploitation, created.TypeBudgetSollicite);
        Assert.Equal("025", created.ItemSollicite);
    }

    [Fact]
    public async Task Creation_Bi_Avec_Item_Obligatoire()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                typeBudgetSollicite: TypeBudgetCode.BudgetInvestissement,
                itemSollicite: "018"));

        Assert.Equal(TypeBudgetCode.BudgetInvestissement, created.TypeBudgetSollicite);
        Assert.Equal("018", created.ItemSollicite);
    }

    [Fact]
    public async Task Creation_Ae_Sans_Item_Refusee()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateBrouillonAsync(
                DemandePaiementTestData.SampleCreateRequest(
                    typeBudgetSollicite: TypeBudgetCode.ActionsExploitation,
                    itemSollicite: null)));

        Assert.Contains("item", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Creation_Bi_Sans_Item_Refusee()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateBrouillonAsync(
                DemandePaiementTestData.SampleCreateRequest(
                    typeBudgetSollicite: TypeBudgetCode.BudgetInvestissement,
                    itemSollicite: " ")));

        Assert.Contains("item", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Creation_Dc_Avec_Item_Refusee()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateBrouillonAsync(
                DemandePaiementTestData.SampleCreateRequest(
                    typeBudgetSollicite: TypeBudgetCode.DepensesCourantes,
                    itemSollicite: "025")));

        Assert.Contains("DC", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ModePaiementDpm.Caisse)]
    [InlineData(ModePaiementDpm.Banque)]
    public async Task Mode_Paiement_Sollicite_Accepte(string mode)
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: mode));

        Assert.Equal(mode, created.ModePaiementSollicite);
    }

    [Fact]
    public async Task Creation_Ne_Cree_Pas_Imputation()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        Assert.Empty(repo.Demandes.Single().Imputations);
    }

    [Fact]
    public async Task Soumettre_Ne_Cree_Pas_Imputation()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var svc = DemandePaiementTestData.CreateService(
            repo,
            [AppPermissions.PaiementsLire, AppPermissions.PaiementsEcrire, AppPermissions.PaiementsSoumettre]);

        var created = await svc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(svc, created.IdDemandePaiement);
        await DemandePaiementTestData.SoumettreApresValidationEntiteAsync(repo, created.IdDemandePaiement, svc);

        Assert.Equal(StatutDemandePaiement.Soumise, repo.Demandes.Single().Statut);
        Assert.Empty(repo.Demandes.Single().Imputations);
    }

    [Fact]
    public async Task Imputation_Junior_Uniquement_En_Controle_Budgetaire()
    {
        var repo = new FakeDemandePaiementRepo();
        var createSvc = DemandePaiementTestData.CreateService(repo);
        var juniorSvc = DemandePaiementTestData.CreateService(repo, AppPermissions.JuniorDc);

        var created = await createSvc.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            juniorSvc.AddImputationAsync(created.IdDemandePaiement, DemandePaiementTestData.ImputationDc()));
    }

    [Fact]
    public async Task Ancienne_Dpm_Sans_Sollicitation_Restent_Lisibles()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Demandes.Add(new BudgetWeb.Domain.Entities.DemandePaiement
        {
            IdDemandePaiement = 99,
            Reference = "DP-LEGACY-00001",
            DateEmission = new DateOnly(2025, 1, 1),
            FK_ExerciceBudgetaire = 1,
            FK_UniteBudgetaire = 10,
            FK_Demandeur = 1,
            FK_CasDossier = 1,
            Objet = "Demande historique",
            MontantBrut = 500m,
            Devise = "USD",
            Statut = StatutDemandePaiement.Soumise,
            FK_UtilisateurCreation = 1,
            DateCreation = DateTime.UtcNow,
        });

        var svc = DemandePaiementTestData.CreateService(repo);
        var detail = await svc.GetByIdAsync(99);

        Assert.NotNull(detail);
        Assert.Null(detail!.TypeBudgetSollicite);
        Assert.Null(detail.ItemSollicite);
        Assert.Equal("DP-LEGACY-00001", detail.Reference);
    }

    [Fact]
    public void FormaterDestination_Pour_Impression()
    {
        Assert.Equal("DC", DestinationBudgetaireSolliciteeRules.FormaterDestination("DC", null));
        Assert.Equal("AE — Item N° 025", DestinationBudgetaireSolliciteeRules.FormaterDestination("AE", "025"));
        Assert.Equal("BI / IVT — Item N° 018", DestinationBudgetaireSolliciteeRules.FormaterDestination("BI", "018"));
    }
}
