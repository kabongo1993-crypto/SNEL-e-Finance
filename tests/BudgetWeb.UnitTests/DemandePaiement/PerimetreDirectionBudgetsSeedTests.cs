using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Préparation du seed périmètre Direction des Budgets (Tous*).
/// </summary>
public class PerimetreDirectionBudgetsSeedTests
{
    [Theory]
    [InlineData(AppRoles.ChargeDp)]
    [InlineData(AppRoles.ChargeDpm)]
    [InlineData(AppRoles.GestionnaireJunior)]
    [InlineData(AppRoles.GestionnaireJuniorDc)]
    [InlineData(AppRoles.GestionnaireSenior)]
    [InlineData(AppRoles.ChefDivision)]
    [InlineData(AppRoles.ControleBudget)]
    [InlineData(AppRoles.DirecteurBudgets)]
    public void EstDirectionBudgets_Accepte_Profils_Cibles_Et_Historiques(string code)
        => Assert.True(ProfilUtilisateurCodes.EstDirectionBudgets(code));

    [Theory]
    [InlineData(AppRoles.ServiceDemandeur)]
    [InlineData(AppRoles.ResponsableServiceDemandeur)]
    [InlineData(AppRoles.ResponsableEntiteInitiatrice)]
    [InlineData(AppRoles.Demandeur)]
    [InlineData(AppRoles.AdministrateurSysteme)]
    [InlineData(AppRoles.Admin)]
    public void EstDirectionBudgets_Refuse_Entite_Initiatrice_Et_Admin(string code)
        => Assert.False(ProfilUtilisateurCodes.EstDirectionBudgets(code));

    [Fact]
    public void Seed_Direction_Est_Configure_Tous()
    {
        var seed = ProfilUtilisateurCodes.PerimetreSeedDirectionBudgets();
        Assert.True(PerimetreAccess.EstConfigure(seed));
        Assert.True(seed.TousDepartements);
        Assert.True(seed.ToutesUnitesBudgetaires);
        Assert.Empty(seed.IdDepartements);
        Assert.Empty(seed.IdUnitesBudgetaires);
        Assert.True(PerimetreAccess.PeutAccederUb(seed, idUb: 999, idDepartementUb: 42));
    }

    [Fact]
    public async Task Circuit_Avec_Seed_Tous_Autorise_Ub_Sans_Proxy()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.Perimetre = ProfilUtilisateurCodes.PerimetreSeedDirectionBudgets();
        repo.UbProxyPrevision.Clear();
        repo.UbAutorisees.Clear();

        // ServiceDemandeur + périmètre Tous* (cas mixte / seed appliqué)
        var service = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

        var created = await service.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(450m));
        Assert.NotNull(created);
    }
}
