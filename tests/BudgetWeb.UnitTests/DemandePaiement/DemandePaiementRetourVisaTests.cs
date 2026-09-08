using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DemandePaiementRetourVisaTests
{
    [Fact]
    public async Task V_Rejet_Retourne_Z_En_Controle()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);

        var id = repo.Demandes.Single().IdDemandePaiement;
        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.AddImputationAsync(id, DemandePaiementTestData.ImputationDc(montantUsd: 5_000m, idBudgetLigne: 100));

        var v = DemandePaiementRoutageTestHelpers.Viseur(repo);
        await v.RetournerAsync(id, new RetourDemandePaiementRequest("Rejet visa", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(StatutDemandePaiement.EnControleBudgetaire, tracked.Statut);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);

        var z2 = DemandePaiementRoutageTestHelpers.Junior(repo, 302);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => z2.GetByIdAsync(id));

        Assert.NotNull(await z.GetByIdAsync(id));
    }
}
