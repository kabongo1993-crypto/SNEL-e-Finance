using BudgetWeb.Application.DpmConsultation;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.DemandePaiement;

/// <summary>
/// Lot 3.6.6 — le destinataire historique provient du routage, pas de l'assignation courante.
/// </summary>
public class DemandePaiementLot366RetourHistoriqueTests
{
    [Fact]
    public async Task Destinataire_Historique_Conserve_Apres_Reassignation()
    {
        var repo = new FakeDemandePaiementRepo();
        repo.Previsions[100] = DemandePaiementTestData.PrevisionDc(montantAnnuel: 200_000m);
        await DemandePaiementRoutageTestHelpers.PreparerEnControleOrienteeAsync(repo);
        var id = repo.Demandes.Single().IdDemandePaiement;

        var z = DemandePaiementRoutageTestHelpers.Junior(repo);
        await z.RetournerAsync(id, new RetourDemandePaiementRequest("Correction imputation", null));

        var retourRoutage = repo.Routages.Single(r =>
            r.FK_DemandePaiement == id
            && r.Action == DemandePaiementRoutageAction.RetourInterEtapes);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, retourRoutage.FK_UtilisateurCible);

        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.OrienterAsync(id, new OrienterDemandePaiementRequest(DemandePaiementRoutageTestHelpers.Z1));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Z1, tracked.FK_UtilisateurAssigne);
        Assert.NotEqual(retourRoutage.FK_UtilisateurCible, tracked.FK_UtilisateurAssigne);

        var retour = Assert.Single(await z.GetRetoursDestinatairesAsync(id)!);
        Assert.Equal(retourRoutage.IdRoutage, retour.IdRoutage);
        Assert.Equal(DemandePaiementRoutageTestHelpers.Y1, retour.IdUtilisateurDestinataire);
        Assert.Equal(retourRoutage.FK_UtilisateurCible, retour.IdUtilisateurDestinataire);
        Assert.Equal(DemandePaiementRetourType.JuniorVersCharge, retour.TypeRetour);
    }

    [Fact]
    public async Task Destinataire_Historique_Independant_De_FK_UtilisateurRetour()
    {
        var repo = new FakeDemandePaiementRepo();
        var id = await DemandePaiementRoutageTestHelpers.CreerSoumiseAsync(repo);
        var y1 = DemandePaiementRoutageTestHelpers.Charge(repo);
        await y1.ReceptionnerAsync(id);
        await y1.RetournerAsync(id, new RetourDemandePaiementRequest("Correction demandeur", null));

        var tracked = DemandePaiementRoutageTestHelpers.Tracked(repo, id);
        tracked.FK_UtilisateurRetour = DemandePaiementRoutageTestHelpers.Y2;
        tracked.FK_UtilisateurAssigne = DemandePaiementRoutageTestHelpers.Y2;

        var retourRoutage = repo.Routages.Single(r =>
            r.FK_DemandePaiement == id
            && r.Action == DemandePaiementRoutageAction.RetourDemandeur);

        var x1 = DemandePaiementRoutageTestHelpers.Demandeur(repo);
        var retour = Assert.Single(await x1.GetRetoursDestinatairesAsync(id)!);

        Assert.Equal(DemandePaiementRoutageTestHelpers.X1, retour.IdUtilisateurDestinataire);
        Assert.Equal(retourRoutage.FK_UtilisateurCible, retour.IdUtilisateurDestinataire);
        Assert.NotEqual(tracked.FK_UtilisateurRetour, retour.IdUtilisateurDestinataire);
        Assert.NotEqual(tracked.FK_UtilisateurAssigne, retour.IdUtilisateurDestinataire);
    }
}
