using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;
using BudgetWeb.Domain.Security;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Vérifie que le POST d'établissement ne recharge plus la DPM via GetDetailAsync.
/// </summary>
public class EtablissementPostSansGetDetailTests
{
    [Fact]
    public async Task EtablirPieceCaisse_Reussit_SansGetDetailAsync()
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

        repo.ResetPdfPipelineCounters();
        var piece = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, piece.Statut);
        Assert.True(piece.IdPieceCaisse > 0);
        Assert.StartsWith("PC-", piece.NumeroPiece, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EtablirBonProvisoire_Reussit_SansGetDetailAsync()
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

        repo.ResetPdfPipelineCounters();
        var bon = await charge.EtablirBonProvisoireAsync(
            created.IdDemandePaiement,
            new EtablirBonProvisoireRequest());

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, bon.Statut);
        Assert.True(bon.IdBonProvisoire > 0);
        Assert.StartsWith("BP-", bon.NumeroBon, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EtablirMinuteCheque_Reussit_SansGetDetailAsync()
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

        repo.ResetPdfPipelineCounters();
        var minute = await charge.EtablirMinuteChequeAsync(
            created.IdDemandePaiement,
            new EtablirMinuteChequeRequest());

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, minute.Statut);
        Assert.True(minute.IdMinuteCheque > 0);
        Assert.StartsWith("OP-", minute.NumeroOp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EtablirBilletConversion_Reussit_SansGetDetailAsync()
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

        repo.ResetPdfPipelineCounters();
        var billet = await charge.EtablirBilletConversionAsync(
            created.IdDemandePaiement,
            new EtablirBilletConversionRequest());

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutBilletConversion.Etabli, billet.Statut);
        Assert.True(billet.IdBilletConversion > 0);
        Assert.Equal("USD", billet.DeviseOrigine);
    }

    [Fact]
    public async Task EtablirPieceCaisse_Idempotent_SansGetDetailAsync()
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

        repo.ResetPdfPipelineCounters();
        var second = await charge.EtablirPieceCaisseAsync(
            created.IdDemandePaiement,
            new EtablirPieceCaisseRequest());

        Assert.Equal(0, repo.GetDetailAsyncCallCount);
        Assert.Equal(StatutDocumentInstrumentPaiement.Etabli, second.Statut);
    }

    [Fact]
    public async Task EtablirPieceCaisse_ErreursMetier_SansGetDetailAsync()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.SeedPieceObligatoire();
        var charge = DemandePaiementTestData.CreateService(repo, AppPermissions.ChargeDpm);

        repo.ResetPdfPipelineCounters();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirPieceCaisseAsync(999_999, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);

        var created = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        repo.ResetPdfPipelineCounters();
        // Statut brouillon : accès Traiter refusé avant ExigerStatut.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);

        await charge.EntrerTraitementAsync(created.IdDemandePaiement);
        repo.ResetPdfPipelineCounters();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);

        repo.SeedParametreInstrument(TypeInstrumentPaiement.PieceCaisse);
        var sansCharge = DemandePaiementTestData.CreateService(
            repo,
            AppPermissions.PermissionsPourProfil(AppRoles.Demandeur));
        repo.ResetPdfPipelineCounters();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sansCharge.EtablirPieceCaisseAsync(created.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);

        var soumise = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "CDF",
                modePaiementSollicite: ModePaiementDpm.Caisse));
        repo.Demandes.First(d => d.IdDemandePaiement == soumise.IdDemandePaiement).Statut =
            StatutDemandePaiement.Soumise;
        repo.ResetPdfPipelineCounters();
        // Statut soumise : accès Traiter refusé (même sémantique métier « mauvais statut »).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            charge.EtablirPieceCaisseAsync(soumise.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);

        var banque = await charge.CreateBrouillonAsync(
            DemandePaiementTestData.SampleCreateRequest(
                devise: "USD",
                modePaiementSollicite: ModePaiementDpm.Banque));
        await charge.EntrerTraitementAsync(banque.IdDemandePaiement);
        repo.ResetPdfPipelineCounters();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            charge.EtablirPieceCaisseAsync(banque.IdDemandePaiement, new EtablirPieceCaisseRequest()));
        Assert.Equal(0, repo.GetDetailAsyncCallCount);
    }
}
