using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class MinuteChequePdfPipelineTests
{
    [Fact]
    public async Task Cas1_MinuteEtablie_GenererPdf_SansGetDetailAsync()
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
        await charge.EtablirMinuteChequeAsync(created.IdDemandePaiement, new EtablirMinuteChequeRequest());

        repo.ResetPdfPipelineCounters();

        var pdf = await charge.GenererMinuteChequePdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetMinuteChequePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas2_MinuteNonEtablie_RetourneErreurMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Banque));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.GenererMinuteChequePdfAsync(created.IdDemandePaiement));

        Assert.Contains("pas encore été établie", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetMinuteChequePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas3_DpmInaccessible_RefuseAcces_SansLectureInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.MinuteCheque);
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Banque));
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation =
            DemandePaiementRoutageTestHelpers.X1;
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await admin.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await admin.SoumettreAsync(created.IdDemandePaiement);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(created.IdDemandePaiement);
        await y1.EtablirMinuteChequeAsync(created.IdDemandePaiement, new EtablirMinuteChequeRequest());

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.GenererMinuteChequePdfAsync(created.IdDemandePaiement));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetMinuteChequePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas4_DpmInexistante_NotFound_SansRequeteInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            charge.GenererMinuteChequePdfAsync(999_999));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetMinuteChequePdfDataAsyncCallCount);
    }

    [Fact]
    public void MapMinuteChequeDocument_ProduitMemeDtoDepuisPdfData()
    {
        var pdfData = new MinuteChequePdfData(
            "MC-2026-00001",
            new DateOnly(2026, 3, 15),
            "DP-2026-00050",
            "Objet test",
            "Jean Dupont",
            "Adresse test",
            "Banque test",
            "123456",
            1500.50m,
            "USD",
            "MILLE CINQ CENTS DOLLARS",
            "CG-01",
            "CPCA-01",
            "LS-01",
            "SEC-01",
            "APP-01",
            100m,
            "MC-2026-00001 · DPM-000050",
            new PdfUserDisplayName("Kabongo", "Paul", "p.kabongo"));

        var entity = new Domain.Entities.MinuteCheque
        {
            NumeroOp = pdfData.NumeroOp,
            DateDocument = pdfData.DateDocument,
            ReferenceDemande = pdfData.ReferenceDemande,
            Motif = pdfData.Motif,
            BeneficiaireAffichage = pdfData.BeneficiaireAffichage,
            BeneficiaireAdresse = pdfData.BeneficiaireAdresse,
            BeneficiaireBanque = pdfData.BeneficiaireBanque,
            BeneficiaireNumeroCompte = pdfData.BeneficiaireNumeroCompte,
            MontantPaiement = pdfData.MontantPaiement,
            DevisePaiement = pdfData.DevisePaiement,
            MontantEnLettres = pdfData.MontantEnLettres,
            CompteGeneral = pdfData.CompteGeneral,
            CpCa = pdfData.CpCa,
            Ls = pdfData.Ls,
            SuiviExtraComptable = pdfData.SuiviExtraComptable,
            NumeroAppariement = pdfData.NumeroAppariement,
            MontantSuiviExtraComptable = pdfData.MontantSuiviExtraComptable,
            IdentifiantVerification = pdfData.IdentifiantVerification,
            UtilisateurEtabli = new Domain.Entities.Utilisateur
            {
                Nom = "Kabongo",
                Prenom = "Paul",
                NomUtilisateur = "p.kabongo",
            },
        };

        var fromPdf = InvokeMapMinuteChequeDocument(pdfData);
        var fromEntity = InvokeMapMinuteChequeDocument(entity);

        Assert.Equal(fromEntity with { DateImpression = fromPdf.DateImpression }, fromPdf with { DateImpression = fromPdf.DateImpression });
        Assert.Equal(fromEntity.NumeroOp, fromPdf.NumeroOp);
        Assert.Equal(fromEntity.MontantPaiement, fromPdf.MontantPaiement);
        Assert.Equal(fromEntity.EtabliPar, fromPdf.EtabliPar);
    }

    private static MinuteChequeDocumentDto InvokeMapMinuteChequeDocument(MinuteChequePdfData data)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapMinuteChequeDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(MinuteChequePdfData)],
            null);
        Assert.NotNull(method);
        return (MinuteChequeDocumentDto)method!.Invoke(null, [data])!;
    }

    private static MinuteChequeDocumentDto InvokeMapMinuteChequeDocument(Domain.Entities.MinuteCheque minute)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapMinuteChequeDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(Domain.Entities.MinuteCheque)],
            null);
        Assert.NotNull(method);
        return (MinuteChequeDocumentDto)method!.Invoke(null, [minute])!;
    }
}
