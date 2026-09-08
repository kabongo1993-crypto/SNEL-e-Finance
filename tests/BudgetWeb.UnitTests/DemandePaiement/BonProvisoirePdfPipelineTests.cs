using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

public class BonProvisoirePdfPipelineTests
{
    [Fact]
    public async Task Cas1_BonEtabli_GenererPdf_SansGetDetailAsync()
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
        await charge.EtablirBonProvisoireAsync(created.IdDemandePaiement, new EtablirBonProvisoireRequest());

        repo.ResetPdfPipelineCounters();

        var pdf = await charge.GenererBonProvisoirePdfAsync(created.IdDemandePaiement);

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetBonProvisoirePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas2_BonNonEtabli_RetourneErreurMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        await charge.EntrerTraitementAsync(created.IdDemandePaiement);

        repo.ResetPdfPipelineCounters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.GenererBonProvisoirePdfAsync(created.IdDemandePaiement));

        Assert.Contains("pas encore été établi", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(1, repo.GetBonProvisoirePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas3_DpmInaccessible_RefuseAcces_SansLectureInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        repo.SeedParametreInstrument(TypeInstrumentPaiement.BonProvisoire);
        DemandePaiementRoutageTestHelpers.SeedUb(repo);

        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation =
            DemandePaiementRoutageTestHelpers.X1;
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await admin.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await admin.SoumettreAsync(created.IdDemandePaiement);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(created.IdDemandePaiement);
        await y1.EtablirBonProvisoireAsync(created.IdDemandePaiement, new EtablirBonProvisoireRequest());

        var y2 = DemandePaiementRoutageTestHelpers.Charge(repo, DemandePaiementRoutageTestHelpers.Y2);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            y2.GenererBonProvisoirePdfAsync(created.IdDemandePaiement));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetBonProvisoirePdfDataAsyncCallCount);
    }

    [Fact]
    public async Task Cas4_DpmInexistante_NotFound_SansRequeteInstrument()
    {
        var repo = new FakeDemandePaiementRepo();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);
        repo.ResetPdfPipelineCounters();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            charge.GenererBonProvisoirePdfAsync(999_999));

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(1, repo.GetDemandeAccesContextAsyncCallCount);
        Assert.Equal(0, repo.GetBonProvisoirePdfDataAsyncCallCount);
    }

    [Fact]
    public void MapBonProvisoireDocument_ProduitMemeDtoDepuisPdfData()
    {
        var pdfData = new BonProvisoirePdfData(
            "BP-2026-00001",
            new DateOnly(2026, 3, 15),
            "DP-2026-00050",
            "Objet test",
            "Justification",
            "Jean Dupont",
            "M123",
            "Jean Dupont",
            "Direction test",
            1500.50m,
            "MILLE CINQ CENTS FRANCS",
            "Reçu caisse",
            "CG-01",
            "CP-01",
            "APP-01",
            "BP-2026-00001 · DPM-000050",
            new PdfUserDisplayName("Kabongo", "Paul", "p.kabongo"));

        var entity = new Domain.Entities.BonProvisoire
        {
            NumeroBon = pdfData.NumeroBon,
            DateBon = pdfData.DateBon,
            ReferenceDemande = pdfData.ReferenceDemande,
            Motif = pdfData.Motif,
            MentionJustificationRetrait = pdfData.MentionJustificationRetrait,
            BeneficiaireAffichage = pdfData.BeneficiaireAffichage,
            BeneficiaireMatricule = pdfData.BeneficiaireMatricule,
            BeneficiaireIdentite = pdfData.BeneficiaireIdentite,
            DirectionBeneficiaire = pdfData.DirectionBeneficiaire,
            MontantFc = pdfData.MontantFc,
            MontantEnLettres = pdfData.MontantEnLettres,
            RecuCaisseCentrale = pdfData.RecuCaisseCentrale,
            CompteGeneral = pdfData.CompteGeneral,
            CompteParticulier = pdfData.CompteParticulier,
            NumeroAppariement = pdfData.NumeroAppariement,
            IdentifiantVerification = pdfData.IdentifiantVerification,
            UtilisateurEtabli = new Domain.Entities.Utilisateur
            {
                Nom = "Kabongo",
                Prenom = "Paul",
                NomUtilisateur = "p.kabongo",
            },
        };

        var fromPdf = InvokeMapBonProvisoireDocument(pdfData);
        var fromEntity = InvokeMapBonProvisoireDocument(entity);

        Assert.Equal(fromEntity with { DateImpression = fromPdf.DateImpression }, fromPdf with { DateImpression = fromPdf.DateImpression });
        Assert.Equal(fromEntity.NumeroBon, fromPdf.NumeroBon);
        Assert.Equal(fromEntity.MontantFc, fromPdf.MontantFc);
        Assert.Equal(fromEntity.EtabliPar, fromPdf.EtabliPar);
    }

    private static BonProvisoireDocumentDto InvokeMapBonProvisoireDocument(BonProvisoirePdfData data)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapBonProvisoireDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(BonProvisoirePdfData)],
            null);
        Assert.NotNull(method);
        return (BonProvisoireDocumentDto)method!.Invoke(null, [data])!;
    }

    private static BonProvisoireDocumentDto InvokeMapBonProvisoireDocument(Domain.Entities.BonProvisoire bon)
    {
        var method = typeof(DemandePaiementService).GetMethod(
            "MapBonProvisoireDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            [typeof(Domain.Entities.BonProvisoire)],
            null);
        Assert.NotNull(method);
        return (BonProvisoireDocumentDto)method!.Invoke(null, [bon])!;
    }
}
