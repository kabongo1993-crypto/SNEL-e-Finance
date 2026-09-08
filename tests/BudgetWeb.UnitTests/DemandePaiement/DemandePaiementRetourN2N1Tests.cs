using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementRetourN2N1Tests
{
    [Fact]
    public async Task N2_Rejet_Retourne_N1_Nominatif()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var n1Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        var n2Svc = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N2,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableEntiteInitiatrice));

        var created = await x1.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        await DemandePaiementTestData.AddSamplePieceAsync(x1, created.IdDemandePaiement);
        await x1.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await n1Svc.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);

        var id = created.IdDemandePaiement;
        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        var n1Row = tracked.ValidationsEntite.Single(v => v.Niveau == ValidationEntiteNiveau.N1);
        Assert.Equal(StatutValidationEntite.Validee, n1Row.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, n1Row.FK_UtilisateurValidateur);

        await n2Svc.RejeterValidationEntiteAsync(
            id,
            new RetourDemandePaiementRequest("Correction entité", null));

        tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnValidationN1, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.N1, tracked.FK_UtilisateurAssigne);

        var n1Apres = tracked.ValidationsEntite.Single(v => v.Niveau == ValidationEntiteNiveau.N1);
        var n2Apres = tracked.ValidationsEntite.Single(v => v.Niveau == ValidationEntiteNiveau.N2);
        Assert.Equal(StatutValidationEntite.Validee, n1Apres.Statut);
        Assert.Equal(StatutValidationEntite.EnAttente, n2Apres.Statut);
        Assert.Null(n2Apres.FK_UtilisateurValidateur);

        var n1Autre = DemandePaiementRoutageTestHelpers.Service(
            repo,
            DemandePaiementRoutageTestHelpers.N1Autre,
            AppPermissions.PermissionsPourProfil(AppRoles.ResponsableServiceDemandeur));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => n1Autre.GetByIdAsync(id));

        var x1Detail = await x1.GetByIdAsync(id);
        Assert.NotNull(x1Detail);

        var relance = await n1Svc.ValiderN1ElectroniqueAsync(id);
        Assert.Equal(StatutDemandePaiement.EnValidationN2, relance.Statut);
        Assert.Null(DemandePaiementRoutageTestHelpers.Tracked(repo, id).FK_UtilisateurAssigne);
    }
}
