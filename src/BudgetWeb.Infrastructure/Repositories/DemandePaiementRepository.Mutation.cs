using BudgetWeb.Application.DTOs;

using BudgetWeb.Domain.Entities;

using Microsoft.EntityFrameworkCore;



namespace BudgetWeb.Infrastructure.Repositories;



public sealed partial class DemandePaiementRepository

{

    public Task<DemandePaiementMutationHeaderReadModel?> GetMutationHeaderAsync(

        long idDemande,

        CancellationToken cancellationToken = default)

        => TrackRepoAsync("GetMutationHeaderAsync", () =>

            _context.DemandesPaiement.AsNoTracking()

                .Where(d => d.IdDemandePaiement == idDemande)

                .Select(d => new DemandePaiementMutationHeaderReadModel(

                    d.IdDemandePaiement,

                    d.Statut,

                    d.FK_UniteBudgetaire,

                    d.FK_UtilisateurCreation,

                    d.FK_UtilisateurAssigne,

                    d.TypeBudgetSollicite,

                    d.TypeBudget != null ? d.TypeBudget.CodeType : null,

                    d.FK_Demandeur,

                    d.Objet,

                    d.MontantBrut,

                    d.FK_CasDossier,

                    d.Devise,

                    d.ItemSollicite,

                    d.ModePaiementSollicite ?? string.Empty))

                .FirstOrDefaultAsync(cancellationToken));



    /// <summary>

    /// Lecture DTO post-mutation générique — pipeline consultation (sans DetailQuery / Snapshots).

    /// </summary>

    public async Task<DemandePaiement?> GetDetailDtoApresMutationAsync(

        long idDemande,

        CancellationToken cancellationToken = default)

    {

        var demande = await TrackRepoAsync(

            "GetDetailDtoApresMutationAsync",

            () => GetDetailConsultationAsync(idDemande, cancellationToken));

        if (demande is null)

            return null;



        var assignation = await _context.DemandesPaiement.AsNoTracking()

            .Where(d => d.IdDemandePaiement == idDemande)

            .Select(d => new

            {

                d.FK_UtilisateurAssigne,

                d.UtilisateurAssigne,

            })

            .FirstOrDefaultAsync(cancellationToken);



        if (assignation is not null)

        {

            demande.FK_UtilisateurAssigne = assignation.FK_UtilisateurAssigne;

            demande.UtilisateurAssigne = assignation.UtilisateurAssigne;

        }



        return demande;

    }



    /// <summary>

    /// Vague 2b-2 — réutilise collections tracked + projection en-tête pour MapDetail.

    /// </summary>

    public Task<DemandePaiement> GetDetailDtoApresEnvoyerAsync(

        DemandePaiement tracked,

        CancellationToken cancellationToken = default)

        => TrackRepoAsync("GetDetailDtoApresEnvoyerAsync", async () =>

        {

            var idDemande = tracked.IdDemandePaiement;

            var header = await QueryMapDetailHeaderShellAsync(idDemande, cancellationToken)

                ?? throw new InvalidOperationException("Demande de paiement introuvable.");



            ApplyMapDetailHeaderShell(tracked, header);

            await EnrichImputationTypeBudgetAsync(tracked, cancellationToken);

            return tracked;

        });



    private Task<DemandePaiement?> QueryMapDetailHeaderShellAsync(

        long idDemande,

        CancellationToken cancellationToken)

        => _context.DemandesPaiement.AsNoTracking()

            .Where(d => d.IdDemandePaiement == idDemande)

            .Select(d => new DemandePaiement

            {

                IdDemandePaiement = d.IdDemandePaiement,

                Reference = d.Reference,

                DateEmission = d.DateEmission,

                LieuEmission = d.LieuEmission,

                FK_ExerciceBudgetaire = d.FK_ExerciceBudgetaire,

                FK_VersionBudgetaire = d.FK_VersionBudgetaire,

                FK_UniteBudgetaire = d.FK_UniteBudgetaire,

                FK_Demandeur = d.FK_Demandeur,

                FK_CasDossier = d.FK_CasDossier,

                FK_TypeBudget = d.FK_TypeBudget,

                TypeBudgetSollicite = d.TypeBudgetSollicite,

                ItemSollicite = d.ItemSollicite,

                Objet = d.Objet,

                CompteSection = d.CompteSection,

                MontantBrut = d.MontantBrut,

                Devise = d.Devise,

                FK_Devise = d.FK_Devise,

                TauxConversion = d.TauxConversion,

                MontantUsd = d.MontantUsd,

                FK_TauxChange = d.FK_TauxChange,

                ModePaiementSollicite = d.ModePaiementSollicite,

                TypeInstrumentPaiement = d.TypeInstrumentPaiement,

                DevisePaiement = d.DevisePaiement,

                MontantPaiement = d.MontantPaiement,

                TauxPaiement = d.TauxPaiement,

                FK_TauxChangePaiement = d.FK_TauxChangePaiement,

                Statut = d.Statut,

                MotifRetour = d.MotifRetour,

                CommentaireRetour = d.CommentaireRetour,

                FK_UtilisateurCreation = d.FK_UtilisateurCreation,

                DateCreation = d.DateCreation,

                DateSoumission = d.DateSoumission,

                DateReception = d.DateReception,

                DateControle = d.DateControle,

                DateVisa = d.DateVisa,

                DateRetour = d.DateRetour,

                FK_UtilisateurAssigne = d.FK_UtilisateurAssigne,

                UtilisateurAssigne = d.FK_UtilisateurAssigne == null

                    ? null

                    : new Utilisateur

                    {

                        IdUtilisateur = d.UtilisateurAssigne!.IdUtilisateur,

                        Nom = d.UtilisateurAssigne.Nom,

                        Prenom = d.UtilisateurAssigne.Prenom,

                    },

                CasDossier = new CasDossier

                {

                    IdCasDossier = d.CasDossier.IdCasDossier,

                    Code = d.CasDossier.Code,

                    Libelle = d.CasDossier.Libelle,

                },

                ExerciceBudgetaire = new ExerciceBudgetaire

                {

                    IdExercice = d.ExerciceBudgetaire.IdExercice,

                    Annee = d.ExerciceBudgetaire.Annee,

                },

                UniteBudgetaire = new UniteBudgetaire

                {

                    IdUB = d.UniteBudgetaire.IdUB,

                    CodeUB = d.UniteBudgetaire.CodeUB,

                    Libelle = d.UniteBudgetaire.Libelle,

                    FK_Departement = d.UniteBudgetaire.FK_Departement,

                    Departement = new Departement

                    {

                        IdDepartement = d.UniteBudgetaire.Departement.IdDepartement,

                        Code = d.UniteBudgetaire.Departement.Code,

                        Libelle = d.UniteBudgetaire.Departement.Libelle,

                    },

                },

                Demandeur = d.Demandeur == null

                    ? null

                    : new Demandeur

                    {

                        IdDemandeur = d.Demandeur.IdDemandeur,

                        Code = d.Demandeur.Code,

                        Libelle = d.Demandeur.Libelle,

                    },

                TypeBudget = d.TypeBudget == null

                    ? null

                    : new TypeBudget

                    {

                        IdTypeBudget = d.TypeBudget.IdTypeBudget,

                        CodeType = d.TypeBudget.CodeType,

                    },

                VersionBudgetaire = d.VersionBudgetaire == null

                    ? null

                    : new VersionBudgetaire

                    {

                        IdVersion = d.VersionBudgetaire.IdVersion,

                        NumeroVersion = d.VersionBudgetaire.NumeroVersion,

                    },

            })

            .FirstOrDefaultAsync(cancellationToken);



    private static void ApplyMapDetailHeaderShell(DemandePaiement tracked, DemandePaiement header)

    {

        tracked.Reference = header.Reference;

        tracked.DateEmission = header.DateEmission;

        tracked.LieuEmission = header.LieuEmission;

        tracked.FK_ExerciceBudgetaire = header.FK_ExerciceBudgetaire;

        tracked.FK_VersionBudgetaire = header.FK_VersionBudgetaire;

        tracked.FK_UniteBudgetaire = header.FK_UniteBudgetaire;

        tracked.FK_Demandeur = header.FK_Demandeur;

        tracked.FK_CasDossier = header.FK_CasDossier;

        tracked.FK_TypeBudget = header.FK_TypeBudget;

        tracked.TypeBudgetSollicite = header.TypeBudgetSollicite;

        tracked.ItemSollicite = header.ItemSollicite;

        tracked.Objet = header.Objet;

        tracked.CompteSection = header.CompteSection;

        tracked.MontantBrut = header.MontantBrut;

        tracked.Devise = header.Devise;

        tracked.FK_Devise = header.FK_Devise;

        tracked.TauxConversion = header.TauxConversion;

        tracked.MontantUsd = header.MontantUsd;

        tracked.FK_TauxChange = header.FK_TauxChange;

        tracked.ModePaiementSollicite = header.ModePaiementSollicite;

        tracked.TypeInstrumentPaiement = header.TypeInstrumentPaiement;

        tracked.DevisePaiement = header.DevisePaiement;

        tracked.MontantPaiement = header.MontantPaiement;

        tracked.TauxPaiement = header.TauxPaiement;

        tracked.FK_TauxChangePaiement = header.FK_TauxChangePaiement;

        tracked.Statut = header.Statut;

        tracked.MotifRetour = header.MotifRetour;

        tracked.CommentaireRetour = header.CommentaireRetour;

        tracked.FK_UtilisateurCreation = header.FK_UtilisateurCreation;

        tracked.DateCreation = header.DateCreation;

        tracked.DateSoumission = header.DateSoumission;

        tracked.DateReception = header.DateReception;

        tracked.DateControle = header.DateControle;

        tracked.DateVisa = header.DateVisa;

        tracked.DateRetour = header.DateRetour;

        tracked.FK_UtilisateurAssigne = header.FK_UtilisateurAssigne;

        tracked.UtilisateurAssigne = header.UtilisateurAssigne;

        tracked.CasDossier = header.CasDossier;

        tracked.ExerciceBudgetaire = header.ExerciceBudgetaire;

        tracked.UniteBudgetaire = header.UniteBudgetaire;

        tracked.Demandeur = header.Demandeur;

        tracked.TypeBudget = header.TypeBudget;

        tracked.VersionBudgetaire = header.VersionBudgetaire;

    }



    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteMinimalAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetValidationsEntiteMinimalAsync", async () =>
        {
            var rows = await _context.DemandePaiementValidations.AsNoTracking()
                .Where(v => v.FK_DemandePaiement == idDemande)
                .OrderBy(v => v.Ordre)
                .Select(v => new DemandePaiementValidation
                {
                    IdValidation = v.IdValidation,
                    FK_DemandePaiement = v.FK_DemandePaiement,
                    Niveau = v.Niveau,
                    Ordre = v.Ordre,
                    Statut = v.Statut,
                })
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<DemandePaiementValidation>)rows;
        });

    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteN2Async(
        long idDemande,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetValidationsEntiteN2Async", async () =>
        {
            var rows = await _context.DemandePaiementValidations.AsNoTracking()
                .Where(v => v.FK_DemandePaiement == idDemande)
                .OrderBy(v => v.Ordre)
                .Select(v => new DemandePaiementValidation
                {
                    IdValidation = v.IdValidation,
                    FK_DemandePaiement = v.FK_DemandePaiement,
                    Niveau = v.Niveau,
                    Ordre = v.Ordre,
                    Statut = v.Statut,
                    ModeValidation = v.ModeValidation,
                })
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<DemandePaiementValidation>)rows;
        });

    public Task<IReadOnlyList<DemandePaiementValidation>> GetValidationsEntiteSoumettreAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("GetValidationsEntiteSoumettreAsync", async () =>
        {
            var rows = await _context.DemandePaiementValidations.AsNoTracking()
                .Where(v => v.FK_DemandePaiement == idDemande)
                .OrderBy(v => v.Ordre)
                .Select(v => new DemandePaiementValidation
                {
                    IdValidation = v.IdValidation,
                    FK_DemandePaiement = v.FK_DemandePaiement,
                    Niveau = v.Niveau,
                    Ordre = v.Ordre,
                    Statut = v.Statut,
                    ModeValidation = v.ModeValidation,
                    EmpreinteDonnees = v.EmpreinteDonnees,
                    FK_UtilisateurValidateur = v.FK_UtilisateurValidateur,
                    FK_UtilisateurDeclarant = v.FK_UtilisateurDeclarant,
                    NomSignatairePhysique = v.NomSignatairePhysique,
                    FonctionSignatairePhysique = v.FonctionSignatairePhysique,
                    DateSignaturePhysique = v.DateSignaturePhysique,
                    DateValidation = v.DateValidation,
                    Commentaire = v.Commentaire,
                    UtilisateurValidateur = v.FK_UtilisateurValidateur == null
                        ? null
                        : new Utilisateur
                        {
                            IdUtilisateur = v.UtilisateurValidateur!.IdUtilisateur,
                            Nom = v.UtilisateurValidateur.Nom,
                        },
                    UtilisateurDeclarant = v.FK_UtilisateurDeclarant == null
                        ? null
                        : new Utilisateur
                        {
                            IdUtilisateur = v.UtilisateurDeclarant!.IdUtilisateur,
                            Nom = v.UtilisateurDeclarant.Nom,
                        },
                })
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<DemandePaiementValidation>)rows;
        });

    public async Task<DemandePaiement?> GetEmpreinteReadAsync(
        long idDemande,
        CancellationToken cancellationToken = default)
    {
        return await TrackRepoAsync("GetEmpreinteReadAsync", async () =>
        {
            var header = await _context.DemandesPaiement.AsNoTracking()
                .Where(d => d.IdDemandePaiement == idDemande)
                .Select(d => new DemandePaiement
                {
                    IdDemandePaiement = d.IdDemandePaiement,
                    DateEmission = d.DateEmission,
                    LieuEmission = d.LieuEmission,
                    FK_Demandeur = d.FK_Demandeur,
                    FK_CasDossier = d.FK_CasDossier,
                    TypeBudgetSollicite = d.TypeBudgetSollicite,
                    ItemSollicite = d.ItemSollicite,
                    Objet = d.Objet,
                    CompteSection = d.CompteSection,
                    MontantBrut = d.MontantBrut,
                    Devise = d.Devise,
                    FK_Devise = d.FK_Devise,
                    ModePaiementSollicite = d.ModePaiementSollicite,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (header is null)
                return null;

            header.Beneficiaires = await _context.DemandePaiementBeneficiaires.AsNoTracking()
                .Where(b => b.FK_DemandePaiement == idDemande)
                .OrderBy(b => b.Ordre)
                .ThenBy(b => b.IdBeneficiaire)
                .ToListAsync(cancellationToken);

            header.PiecesJointes = await _context.PiecesJointes.AsNoTracking()
                .Where(p => p.FK_DemandePaiement == idDemande)
                .ToListAsync(cancellationToken);

            header.Imputations = await _context.DemandePaiementImputations.AsNoTracking()
                .Where(i => i.FK_DemandePaiement == idDemande)
                .OrderBy(i => i.Ordre)
                .ThenBy(i => i.IdImputation)
                .ToListAsync(cancellationToken);

            return header;
        });
    }

    public Task EnrichDemandeMapDetailShellAsync(
        DemandePaiement demande,
        CancellationToken cancellationToken = default)
        => TrackRepoAsync("EnrichDemandeMapDetailShellAsync", async () =>
        {
            var idDemande = demande.IdDemandePaiement;
            var header = await QueryMapDetailHeaderShellAsync(idDemande, cancellationToken)
                ?? throw new InvalidOperationException("Demande de paiement introuvable.");

            ApplyMapDetailHeaderShell(demande, header);
            await EnrichImputationTypeBudgetAsync(demande, cancellationToken);
        });

    private async Task EnrichImputationTypeBudgetAsync(

        DemandePaiement tracked,

        CancellationToken cancellationToken)

    {

        if (tracked.Imputations.Count == 0)

            return;



        if (tracked.Imputations.All(i => i.TypeBudget is not null))

            return;



        var rows = await _context.DemandePaiementImputations.AsNoTracking()

            .Where(i => i.FK_DemandePaiement == tracked.IdDemandePaiement)

            .Select(i => new

            {

                i.IdImputation,

                i.FK_TypeBudget,

                CodeType = i.TypeBudget.CodeType,

            })

            .ToListAsync(cancellationToken);



        foreach (var imputation in tracked.Imputations)

        {

            if (imputation.TypeBudget is not null)

                continue;



            var row = rows.FirstOrDefault(r => r.IdImputation == imputation.IdImputation);

            if (row is null)

                continue;



            imputation.TypeBudget = new TypeBudget

            {

                IdTypeBudget = row.FK_TypeBudget,

                CodeType = row.CodeType,

            };

        }

    }

}


