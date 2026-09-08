using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class DocumentInstrumentPaiementTests
{
    [Fact]
    public async Task GetPieceCaisse_Banque_RetourneNull()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Banque));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var piece = await charge.GetPieceCaisseAsync(created.IdDemandePaiement);
        Assert.Null(piece);
    }

    [Fact]
    public async Task GetMinuteCheque_Caisse_RetourneNull()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var minute = await charge.GetMinuteChequeAsync(created.IdDemandePaiement);
        Assert.Null(minute);
    }

    [Fact]
    public async Task EtablirPieceCaisse_SansParametreActif_Refuse()
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
            charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Contains("paramétrage de la pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("n'est pas configuré", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirBonProvisoire_SansParametreActif_Refuse()
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
            charge.EtablirBonProvisoireAsync(created.IdDemandePaiement, new EtablirBonProvisoireRequest()));
        Assert.Contains("paramétrage du bon provisoire", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("n'est pas configuré", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirMinuteCheque_SansParametreActif_Refuse()
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
            charge.EtablirMinuteChequeAsync(created.IdDemandePaiement, new EtablirMinuteChequeRequest()));
        Assert.Contains("paramétrage de la minute de chèque", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("n'est pas configuré", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirPieceCaisse_ParametreActifIncomplet_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.ParametresInstrument.Add(new ParametreInstrumentPaiement
        {
            TypeInstrument = TypeInstrumentPaiement.PieceCaisse,
            Actif = true,
            Sr = "TEST-SR",
            ComptabiliteGenerale = "",
            Cp = "TEST-CP",
            Cpa = "TEST-CPA",
            NumeroAppariement = "TEST-APP",
            RecuInstitutionnel = "TEST-RECU",
        });
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Contains("paramétrage de la pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("n'est pas configuré", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirPieceCaisse_CdfCaisse_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                montantBrut: 50_000m,
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var piece = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());

        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, piece.Statut);
        Assert.StartsWith("PC-", piece.NumeroPiece, StringComparison.Ordinal);
        Assert.Equal(50_000m, piece.MontantFc);
        Assert.Contains("francs congolais", piece.MontantEnLettres, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirPieceCaisse_UsdCaisse_SansBillet_Refuse()
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
            charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Contains("billet", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirPieceCaisse_Idempotent()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var first = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());
        var second = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());

        Assert.Equal(first.IdPieceCaisse, second.IdPieceCaisse);
        Assert.Equal(first.NumeroPiece, second.NumeroPiece);
    }

    [Fact]
    public async Task EtablirBonProvisoire_ApresPieceCaisse_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirBonProvisoireAsync(created.IdDemandePaiement, new EtablirBonProvisoireRequest()));
        Assert.Contains("pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EtablirBonProvisoire_CdfCaisse_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.BonProvisoire);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var bon = await charge.EtablirBonProvisoireAsync(
            created.IdDemandePaiement,
            new EtablirBonProvisoireRequest());

        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, bon.Statut);
        Assert.StartsWith("BP-", bon.NumeroBon, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EtablirMinuteCheque_Banque_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.MinuteCheque);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Banque));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var minute = await charge.EtablirMinuteChequeAsync(
            created.IdDemandePaiement,
            new EtablirMinuteChequeRequest());

        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, minute.Statut);
        Assert.StartsWith("OP-", minute.NumeroOp, StringComparison.Ordinal);
        Assert.Equal("USD", minute.DevisePaiement);
    }

    [Fact]
    public async Task EtablirMinuteCheque_Caisse_Refuse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirMinuteChequeAsync(created.IdDemandePaiement, new EtablirMinuteChequeRequest()));
        Assert.Contains("CAISSE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TraiterCharge_SansDocumentInstrument_Refuse()
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
            charge.TraiterChargeAsync(
                created.IdDemandePaiement,
                new TraitementChargeDpmRequest(
                    TypeInstrumentPaiement.PieceCaisse,
                    "CDF",
                    null,
                    null)));
        Assert.Contains("document instrument", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TraiterCharge_AvecPieceCaisseEtablie_Reussit()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await DemandePaiementTestData.EtablirDocumentInstrumentStandardAsync(repo, charge,
            created.IdDemandePaiement,
            TypeInstrumentPaiement.PieceCaisse);

        var traite = await charge.TraiterChargeAsync(
            created.IdDemandePaiement,
            new TraitementChargeDpmRequest(
                TypeInstrumentPaiement.PieceCaisse,
                "CDF",
                null,
                null));

        Assert.Equal(TypeInstrumentPaiement.PieceCaisse, traite.TypeInstrumentPaiement);
    }

    [Fact]
    public async Task GenererPieceCaissePdf_RetournePdf()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest());

        var pdf = await charge.GenererPieceCaissePdfAsync(created.IdDemandePaiement);
        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0x50, pdf[1]);
    }

    [Fact]
    public async Task GetDetailComplet_ChargeCdf_InclutPieceCaisse()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest());

        var detail = await charge.GetDetailCompletAsync(created.IdDemandePaiement);
        Assert.NotNull(detail!.PieceCaisse);
        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, detail.PieceCaisse!.Statut);
    }

    [Fact]
    public async Task EtablirPieceCaisse_ParametreModifieApresEtablissement_ConserveValeursFigees()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.ParametresInstrument.Add(new ParametreInstrumentPaiement
        {
            TypeInstrument = TypeInstrumentPaiement.PieceCaisse,
            Actif = true,
            Sr = "SR-PARAM-A",
            ComptabiliteGenerale = "CG-A",
            Cp = "CP-A",
            Cpa = "CPA-A",
            NumeroAppariement = "APP-A",
            RecuInstitutionnel = "Reçu param A",
        });

        var capturingRenderer = new CapturingPieceCaisseDocumentRenderer();
        var charge = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.ChargeDpm,
            pieceCaisseRenderer: capturingRenderer);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        var piece = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());

        Assert.Equal("SR-PARAM-A", piece.Sr);
        Assert.Equal("CG-A", piece.ComptabiliteGenerale);
        Assert.Equal("CP-A", piece.Cp);
        Assert.Equal("CPA-A", piece.Cpa);
        Assert.Equal("APP-A", piece.NumeroAppariement);
        Assert.Equal("Reçu param A", piece.RecuSnel);

        var param = repo.ParametresInstrument.Single();
        param.Sr = "SR-PARAM-B";
        param.ComptabiliteGenerale = "CG-B";
        param.Cp = "CP-B";
        param.Cpa = "CPA-B";
        param.NumeroAppariement = "APP-B";
        param.RecuInstitutionnel = "Reçu param B";

        var reloaded = await charge.GetPieceCaisseAsync(created.IdDemandePaiement);
        Assert.NotNull(reloaded);
        Assert.Equal("SR-PARAM-A", reloaded.Sr);
        Assert.Equal("CG-A", reloaded.ComptabiliteGenerale);
        Assert.Equal("CP-A", reloaded.Cp);
        Assert.Equal("CPA-A", reloaded.Cpa);
        Assert.Equal("APP-A", reloaded.NumeroAppariement);
        Assert.Equal("Reçu param A", reloaded.RecuSnel);

        await charge.GenererPieceCaissePdfAsync(created.IdDemandePaiement);
        Assert.NotNull(capturingRenderer.LastPayload);
        Assert.Equal("SR-PARAM-A", capturingRenderer.LastPayload.Sr);
        Assert.Equal("CG-A", capturingRenderer.LastPayload.ComptabiliteGenerale);
        Assert.Equal("Reçu param A", capturingRenderer.LastPayload.RecuSnel);
    }

    [Fact]
    public async Task RetenirSollicitation_ApresPieceCaisse_VerrouilleMode()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        await charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.RetenirSollicitationChargeAsync(
                created.IdDemandePaiement,
                new RetenirSollicitationChargeRequest(
                    ModePaiementDpm.Banque,
                    TypeBudgetCode.DepensesCourantes)));
        Assert.Contains("pièce de caisse", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
