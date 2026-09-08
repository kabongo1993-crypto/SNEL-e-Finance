using BudgetWeb.Application.DTOs;

using BudgetWeb.Domain.DemandePaiement;

using BudgetWeb.Domain.Enums;

using BudgetWeb.Domain.Security;

using Xunit;



namespace BudgetWeb.UnitTests.DemandePaiement;



public class ModePaiementVerrouillageTests

{

    [Fact]

    public async Task RetenirSollicitation_ChangeMode_AvantBillet_Reussit()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var updated = await charge.RetenirSollicitationChargeAsync(

            created.IdDemandePaiement,

            new RetenirSollicitationChargeRequest(

                ModePaiementSollicite: ModePaiementDpm.Banque,

                TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes));



        Assert.Equal(ModePaiementDpm.Banque, updated.ModePaiementSollicite);

        Assert.False(updated.ModePaiementVerrouille);

    }



    [Fact]

    public async Task RetenirSollicitation_ChangeMode_ApresBilletEtabli_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.RetenirSollicitationChargeAsync(

                created.IdDemandePaiement,

                new RetenirSollicitationChargeRequest(

                    ModePaiementSollicite: ModePaiementDpm.Banque,

                    TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes)));



        Assert.Contains("verrouillé", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task TraiterCharge_InstrumentCoherent_AvecModeActuelApresRetenir()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        await charge.RetenirSollicitationChargeAsync(

            created.IdDemandePaiement,

            new RetenirSollicitationChargeRequest(

                ModePaiementSollicite: ModePaiementDpm.Banque,

                TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes));



        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, created.IdDemandePaiement);



        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.MinuteCheque);



        var traite = await charge.TraiterChargeAsync(

            created.IdDemandePaiement,

            new TraitementChargeDpmRequest(

                TypeInstrumentPaiement.MinuteCheque,

                "USD",

                null,

                null,

                ModePaiementSollicite: ModePaiementDpm.Banque));



        Assert.Equal(ModePaiementDpm.Banque, traite.ModePaiementSollicite);

        Assert.Equal(TypeInstrumentPaiement.MinuteCheque, traite.TypeInstrumentPaiement);

        Assert.True(traite.ModePaiementVerrouille);

    }



    [Fact]

    public async Task TraiterCharge_ModeDifferentApresBilletEtabli_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());

        await DemandePaiementTestData.EtablirDocumentInstrumentStandardAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.PieceCaisse);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.TraiterChargeAsync(

                created.IdDemandePaiement,

                new TraitementChargeDpmRequest(

                    TypeInstrumentPaiement.MinuteCheque,

                    "USD",

                    null,

                    null,

                    ModePaiementSollicite: ModePaiementDpm.Banque)));



        Assert.Contains("verrouillé", ex.Message, StringComparison.OrdinalIgnoreCase);

    }

}


