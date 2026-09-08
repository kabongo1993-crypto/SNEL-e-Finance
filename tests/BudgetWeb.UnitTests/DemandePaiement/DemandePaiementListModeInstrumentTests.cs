using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// La liste doit exposer le mode/instrument déjà enregistrés — le dialogue
/// « Établir les documents » ne doit pas retomber sur un défaut CAISSE.
/// </summary>
public class DemandePaiementListModeInstrumentTests
{
    [Fact]
    public async Task Liste_Expose_ModeCaisse_Et_Instrument()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));
        repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement)
            .TypeInstrumentPaiement = TypeInstrumentPaiement.PieceCaisse;

        var row = Assert.Single(
            await svc.ListAsync(new DemandePaiementQuery()),
            r => r.IdDemandePaiement == created.IdDemandePaiement);

        Assert.Equal(ModePaiementDpm.Caisse, row.ModePaiementSollicite);
        Assert.Equal(TypeInstrumentPaiement.PieceCaisse, row.TypeInstrumentPaiement);
        Assert.NotEqual(ModePaiementDpm.Banque, row.ModePaiementSollicite);
    }

    [Fact]
    public async Task Liste_Expose_ModeBanque_Et_MinuteCheque()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var created = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Banque,
                objet: "Paiement banque"));
        repo.Demandes.Single(d => d.IdDemandePaiement == created.IdDemandePaiement)
            .TypeInstrumentPaiement = TypeInstrumentPaiement.MinuteCheque;

        var list = await svc.ListAsync(new DemandePaiementQuery());
        var row = Assert.Single(list, r => r.IdDemandePaiement == created.IdDemandePaiement);
        var detail = await svc.GetByIdAsync(created.IdDemandePaiement);

        Assert.Equal(ModePaiementDpm.Banque, row.ModePaiementSollicite);
        Assert.Equal(TypeInstrumentPaiement.MinuteCheque, row.TypeInstrumentPaiement);
        Assert.Equal(detail!.ModePaiementSollicite, row.ModePaiementSollicite);
        Assert.Equal(detail.TypeInstrumentPaiement, row.TypeInstrumentPaiement);
    }

    [Fact]
    public async Task Liste_LotHeterogene_Conserve_LeModeDeChaqueDpm()
    {
        var repo = new FakeDemandePaiementRepo();
        var svc = DemandePaiementTestData.CreateService(repo);

        var caisse = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Caisse,
                objet: "Caisse"));
        var banque = await svc.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Banque,
                objet: "Banque"));

        var list = await svc.ListAsync(new DemandePaiementQuery());
        var rowCaisse = Assert.Single(list, r => r.IdDemandePaiement == caisse.IdDemandePaiement);
        var rowBanque = Assert.Single(list, r => r.IdDemandePaiement == banque.IdDemandePaiement);

        Assert.Equal(ModePaiementDpm.Caisse, rowCaisse.ModePaiementSollicite);
        Assert.Equal(ModePaiementDpm.Banque, rowBanque.ModePaiementSollicite);
    }
}
