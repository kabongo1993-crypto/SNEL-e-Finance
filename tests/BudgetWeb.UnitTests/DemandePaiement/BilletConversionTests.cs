using BudgetWeb.Application.DTOs;

using BudgetWeb.Domain.DemandePaiement;

using BudgetWeb.Domain.Enums;

using BudgetWeb.Domain.Referentiels;

using BudgetWeb.Domain.Security;

using Xunit;



namespace BudgetWeb.UnitTests.DemandePaiement;



public class BilletConversionTests

{

    [Theory]

    [InlineData(ModePaiementDpm.Caisse, "USD", true)]

    [InlineData(ModePaiementDpm.Caisse, "EUR", true)]

    [InlineData(ModePaiementDpm.Caisse, "CDF", false)]

    [InlineData(ModePaiementDpm.Banque, "USD", false)]

    [InlineData(ModePaiementDpm.Banque, "EUR", false)]

    [InlineData(ModePaiementDpm.Banque, "CDF", false)]

    public void NecessiteBillet_ModeEtDevise(string mode, string devise, bool attendu)

    {

        Assert.Equal(attendu, BilletConversionRules.NecessiteBillet(mode, devise));

    }



    [Fact]

    public void NecessiteBillet_CdfCaisse_False()

    {

        Assert.False(BilletConversionRules.NecessiteBillet(ModePaiementDpm.Caisse, "CDF"));

        Assert.False(BilletConversionRules.NecessiteBillet(ModePaiementDpm.Caisse, " cdf "));

    }



    [Fact]

    public async Task GetBilletConversion_CdfCaisse_RetourneNull()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "CDF",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.GetBilletConversionAsync(created.IdDemandePaiement);

        Assert.Null(billet);

    }



    [Fact]

    public async Task GetBilletConversion_UsdBanque_RetourneNull()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Banque));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.GetBilletConversionAsync(created.IdDemandePaiement);

        Assert.Null(billet);

    }



    [Fact]

    public async Task EtablirBillet_UsdCaisse_Reussit()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 1_000m,

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());



        Assert.Equal(StatutBilletConversion.Etabli, billet.Statut);

        Assert.Equal("USD", billet.DeviseOrigine);

        Assert.Equal(1_000m, billet.MontantDeviseOrigine);

        Assert.Equal(FakeTauxChangeService.TauxUsdCdf, billet.TauxApplique);

        Assert.Equal(1_000m * FakeTauxChangeService.TauxUsdCdf, billet.MontantCdf);

        Assert.Single(repo.Billets);

    }



    [Fact]

    public async Task EtablirBillet_EurCaisse_Reussit()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                montantBrut: 500m,

                devise: "EUR",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());



        Assert.Equal("EUR", billet.DeviseOrigine);

        Assert.Equal(FakeTauxChangeService.TauxEurCdf, billet.TauxApplique);

        Assert.Equal(500m * FakeTauxChangeService.TauxEurCdf, billet.MontantCdf);

    }



    [Fact]

    public async Task EtablirBillet_UsdBanque_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Banque));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.EtablirBilletConversionAsync(

                created.IdDemandePaiement,

                new EtablirBilletConversionRequest()));



        Assert.Contains("BANQUE", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(repo.Billets);

    }



    [Fact]

    public async Task EtablirBillet_MontantEtTaux_Coherents()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(
                montantBrut: 123.45m,
                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest(DateConversion: new DateOnly(2026, 3, 1)));



        var attendu = decimal.Round(

            123.45m * billet.TauxApplique,

            4,

            MidpointRounding.AwayFromZero);

        Assert.Equal(attendu, billet.MontantCdf);

        Assert.Equal(billet.TauxApplique, billet.MontantCdf / billet.MontantDeviseOrigine, 4);

    }



    [Theory]

    [InlineData(null, null)]

    [InlineData("CHQ-001", null)]

    [InlineData(null, "Banque X")]

    public async Task EtablirBillet_ChampsFacultatifs_Texte(

        string? demandeCheque,

        string? coursBanque)

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest(

                DemandeChequeNumero: demandeCheque,

                CoursEchangeBanque: coursBanque,

                SoldeAPayerDevise: null));



        Assert.Equal(demandeCheque, billet.DemandeChequeNumero);

        Assert.Equal(coursBanque, billet.CoursEchangeBanque);

        Assert.Null(billet.SoldeAPayerDevise);

    }



    [Fact]

    public async Task EtablirBillet_SoldeFacultatif()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var billet = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest(SoldeAPayerDevise: 250m));



        Assert.Equal(250m, billet.SoldeAPayerDevise);

    }



    [Fact]

    public async Task EtablirBillet_DeuxiemeAppel_Idempotent()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var first = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());

        var second = await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest(DemandeChequeNumero: "AUTRE"));



        Assert.Equal(first.IdBilletConversion, second.IdBilletConversion);

        Assert.Single(repo.Billets);

        Assert.Null(second.DemandeChequeNumero);

    }



    [Fact]

    public async Task EtablirBillet_SansPermission_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var lecture = DemandePaiementTestData.CreateService(

            repo,

            [AppPermissions.PaiementsLire]);



        var created = await DemandePaiementTestData.CreateService(repo).CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));

        await DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm)

            .EntrerTraitementAsync(created.IdDemandePaiement);



        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>

            lecture.EtablirBilletConversionAsync(

                created.IdDemandePaiement,

                new EtablirBilletConversionRequest()));

    }



    [Fact]

    public async Task EtablirBillet_CdfCaisse_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "CDF",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.EtablirBilletConversionAsync(

                created.IdDemandePaiement,

                new EtablirBilletConversionRequest()));



        Assert.Contains("requis", ex.Message, StringComparison.OrdinalIgnoreCase);

    }



    [Fact]

    public async Task GenererPdf_ApresEtablissement_RetourneOctets()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());



        var pdf = await charge.GenererBilletConversionPdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);

        Assert.Equal(0x25, pdf[0]);

    }



    [Fact]

    public async Task DetailComplet_UsdCaisseVersBanque_NInclutPlusBillet()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var avant = await charge.GetDetailCompletAsync(created.IdDemandePaiement);

        Assert.Null(avant!.BilletConversion);



        await charge.RetenirSollicitationChargeAsync(

            created.IdDemandePaiement,

            new RetenirSollicitationChargeRequest(

                ModePaiementSollicite: ModePaiementDpm.Banque,

                TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes));



        var apres = await charge.GetDetailCompletAsync(created.IdDemandePaiement);

        Assert.Null(apres!.BilletConversion);

        Assert.False(apres.Demande.ModePaiementVerrouille);

    }



    [Fact]

    public async Task DetailComplet_UsdBanqueVersCaisse_BilletEligible()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Banque));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var avant = await charge.GetDetailCompletAsync(created.IdDemandePaiement);

        Assert.Null(avant!.BilletConversion);



        await charge.RetenirSollicitationChargeAsync(

            created.IdDemandePaiement,

            new RetenirSollicitationChargeRequest(

                ModePaiementSollicite: ModePaiementDpm.Caisse,

                TypeBudgetSollicite: TypeBudgetCode.DepensesCourantes));



        await charge.EtablirBilletConversionAsync(

            created.IdDemandePaiement,

            new EtablirBilletConversionRequest());



        var apres = await charge.GetDetailCompletAsync(created.IdDemandePaiement);

        Assert.NotNull(apres!.BilletConversion);

        Assert.Equal(StatutBilletConversion.Etabli, apres.BilletConversion!.Statut);

    }



    [Fact]

    public async Task TraiterCharge_UsdBanque_SansBillet_Reussit()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Banque));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.MinuteCheque);



        var traite = await charge.TraiterChargeAsync(

            created.IdDemandePaiement,

            new TraitementChargeDpmRequest(

                TypeInstrumentPaiement.MinuteCheque,

                "USD",

                null,

                null));



        Assert.Equal(ModePaiementDpm.Banque, traite.ModePaiementSollicite);

        Assert.Equal(TypeInstrumentPaiement.MinuteCheque, traite.TypeInstrumentPaiement);

    }



    [Fact]

    public async Task TraiterCharge_UsdCaisse_SansBillet_Refuse()

    {

        var repo = new FakeDemandePaiementRepo();

        repo.SeedPieceObligatoire();

        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);



        var created = await charge.CreateBrouillonAsync(

            DemandePaiementTestData.SampleCreateRequest(

                devise: "USD",

                modePaiementSollicite: ModePaiementDpm.Caisse));

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);



        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>

            charge.TraiterChargeAsync(

                created.IdDemandePaiement,

                new TraitementChargeDpmRequest(

                    TypeInstrumentPaiement.PieceCaisse,

                    "CDF",

                    null,

                    null)));



        Assert.Contains("billet", ex.Message, StringComparison.OrdinalIgnoreCase);

    }

}


