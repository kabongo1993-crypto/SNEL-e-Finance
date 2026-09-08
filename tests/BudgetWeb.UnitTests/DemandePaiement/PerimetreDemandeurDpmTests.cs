using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Security;
using DemandePaiementEntity = BudgetWeb.Domain.Entities.DemandePaiement;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Périmètre demandeurs + garde RemettreEnBrouillon (correctifs audit DPM).
/// </summary>
public class PerimetreDemandeurDpmTests
{
    [Fact]
    public async Task ListDemandeur_Filtre_Selon_Perimetre()
    {
        var perimetreRepo = new FakeDemandePaiementRepo();
        perimetreRepo.Perimetre = new PerimetreUtilisateurSnapshot(false, false, [1], [10]);
        perimetreRepo.UbDepartements[10] = 1;
        perimetreRepo.UbDepartements[20] = 2;

        var demandeurs = new FakeDemandeurRepo();
        demandeurs.Ubs[20] = new UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "Autre UB",
            FK_Departement = 2,
            Actif = true,
            Departement = new Departement { IdDepartement = 2, Code = "D2", Libelle = "Dept 2", Actif = true },
        };
        demandeurs.Items.Add(new Demandeur
        {
            IdDemandeur = 2,
            Code = "AUTRE/UB",
            Libelle = "Hors périmètre",
            FK_UniteBudgetaire = 20,
            Actif = true,
            DateCreation = DateTime.UtcNow,
            FK_UtilisateurCreation = 1,
            UniteBudgetaire = demandeurs.Ubs[20],
        });

        var svc = DemandePaiementTestData.CreateDemandeurService(
            demandeurs,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            perimetreRepo);

        var rows = await svc.ListAsync();

        Assert.Single(rows);
        Assert.Equal(10, rows[0].IdUB);
    }

    [Fact]
    public async Task ListDemandeur_Sans_Perimetre_Filtre_Proxy_Prevision()
    {
        var perimetreRepo = new FakeDemandePaiementRepo();
        perimetreRepo.Perimetre = null;
        perimetreRepo.UbProxyPrevision.Clear();
        perimetreRepo.UbProxyPrevision.Add(10);
        perimetreRepo.UbDepartements[10] = 1;
        perimetreRepo.UbDepartements[20] = 2;

        var demandeurs = new FakeDemandeurRepo();
        demandeurs.Ubs[20] = new UniteBudgetaire
        {
            IdUB = 20,
            CodeUB = "UB020",
            Libelle = "Autre UB",
            FK_Departement = 2,
            Actif = true,
            Departement = new Departement { IdDepartement = 2, Code = "D2", Libelle = "Dept 2", Actif = true },
        };
        demandeurs.Items.Add(new Demandeur
        {
            IdDemandeur = 2,
            Code = "AUTRE/UB",
            Libelle = "Sans proxy",
            FK_UniteBudgetaire = 20,
            Actif = true,
            DateCreation = DateTime.UtcNow,
            FK_UtilisateurCreation = 1,
            UniteBudgetaire = demandeurs.Ubs[20],
        });

        var svc = DemandePaiementTestData.CreateDemandeurService(
            demandeurs,
            AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            perimetreRepo);

        var rows = await svc.ListAsync();

        Assert.Single(rows);
        Assert.Equal(10, rows[0].IdUB);
    }

    [Fact]
    public async Task RemettreEnBrouillon_Refuse_Dp_Hors_Acces()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Perimetre = null;
        repo.UbProxyPrevision.Clear();
        repo.Demandes.Add(new DemandePaiementEntity
        {
            IdDemandePaiement = 100,
            Reference = "DP-TEST-001",
            Statut = StatutDemandePaiement.ACorriger,
            FK_UniteBudgetaire = 10,
            FK_UtilisateurCreation = 1,
            FK_Demandeur = 1,
            FK_ExerciceBudgetaire = 1,
            FK_TypeBudget = 1,
            FK_CasDossier = 1,
            MontantBrut = 500m,
            Devise = "USD",
            DateCreation = DateTime.Now,
        });

        var intrus = DemandePaiementTestData.CreateServiceForUser(
            repo,
            new FakeUser
            {
                UserId = 99,
                Permissions = AppPermissions.PermissionsPourProfil(AppRoles.ServiceDemandeur),
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            intrus.RemettreEnBrouillonAsync(100));
    }
}
