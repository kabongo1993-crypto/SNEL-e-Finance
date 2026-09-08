using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>Helpers partagés pour les tests de routage Lot 3.3.</summary>
internal static class DemandePaiementRoutageTestHelpers
{
    internal const long X1 = 10;
    internal const long N1 = 201;
    internal const long N2 = 202;
    internal const long N1Autre = 203;
    internal const long Y1 = 101;
    internal const long Y2 = 102;
    internal const long Z1 = 301;
    internal const long V1 = 401;

    internal static DemandePaiementService Service(
        FakeDemandePaiementRepo repo,
        long userId,
        IReadOnlyList<string> permissions)
        => DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser { UserId = userId, Permissions = permissions });

    internal static DemandePaiementService Demandeur(FakeDemandePaiementRepo repo, long userId = X1)
        => Service(repo, userId, AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur));

    internal static DemandePaiementService Charge(FakeDemandePaiementRepo repo, long userId = Y1)
        => Service(repo, userId, AppPermissions.ChargeDpm);

    internal static DemandePaiementService Junior(FakeDemandePaiementRepo repo, long userId = Z1)
        => Service(repo, userId, AppPermissions.JuniorDc);

    internal static DemandePaiementService Viseur(FakeDemandePaiementRepo repo, long userId = V1)
        => Service(repo, userId, AppPermissions.PermissionsPourProfil(AppRoles.ChefDivision));

    internal static void SeedUb(FakeDemandePaiementRepo repo)
    {
        repo.UbDepartements[10] = 1;
        repo.UbAutorisees.Add(10);
        repo.UbProxyPrevision.Add(10);
    }

    internal static async Task<long> CreerSoumiseAsync(FakeDemandePaiementRepo repo, long creatorId = X1)
    {
        repo.SeedPieceObligatoire();
        SeedUb(repo);
        var admin = DemandePaiementTestData.CreateService(repo, AppPermissions.AdminFull);
        var created = await admin.CreateBrouillonAsync(DemandePaiementTestData.SampleCreateRequest());
        DemandePaiementRoutageTestHelpers.Tracked(repo, created.IdDemandePaiement).FK_UtilisateurCreation = creatorId;
        await DemandePaiementTestData.AddSamplePieceAsync(admin, created.IdDemandePaiement);
        await admin.EnvoyerEnValidationAsync(created.IdDemandePaiement);
        await admin.ValiderN1ElectroniqueAsync(created.IdDemandePaiement);
        await admin.ValiderN2ElectroniqueAsync(created.IdDemandePaiement);
        await admin.SoumettreAsync(created.IdDemandePaiement);
        return created.IdDemandePaiement;
    }

    internal static async Task PreparerEnControleOrienteeAsync(
        FakeDemandePaiementRepo repo,
        long chargeId = Y1,
        long juniorId = Z1)
    {
        var id = await CreerSoumiseAsync(repo);
        var charge = Charge(repo, chargeId);
        await charge.ReceptionnerAsync(id);
        await DemandePaiementTestData.AddSampleInstrumentPieceAsync(repo, charge, id);
        await DemandePaiementTestData.EtablirBilletConversionStandardAsync(charge, id);
        await charge.TraiterChargeAsync(
            id,
            new TraitementChargeDpmRequest("PIECE_CAISSE", "CDF", null, null));
        await charge.OrienterAsync(id, new OrienterDemandePaiementRequest(juniorId));
    }

    internal static DemandePaiementEntity Tracked(FakeDemandePaiementRepo repo, long id)
        => repo.Demandes.Single(d => d.IdDemandePaiement == id);

    internal static DemandePaiementRoutage? RoutageActif(FakeDemandePaiementRepo repo, long id)
        => repo.Routages.LastOrDefault(r => r.FK_DemandePaiement == id && r.EstActif);
}
