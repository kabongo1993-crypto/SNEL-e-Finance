using BudgetWeb.Application.DTOs.Referentiels;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Interfaces.Referentiels;
using BudgetWeb.Application.Referentiels;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Domain.Referentiels;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class TauxChangeService : ITauxChangeService
{
    private readonly ITauxChangeRepository _repository;
    private readonly IPaireTauxChangeRepository _paires;
    private readonly ICurrentUserService _currentUser;

    public TauxChangeService(
        ITauxChangeRepository repository,
        IPaireTauxChangeRepository paires,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _paires = paires;
        _currentUser = currentUser;
    }

    public IReadOnlyList<PaireTauxChangeDto> ListerPairesSupportees()
        => ListerPairesSupporteesAsync().GetAwaiter().GetResult();

    public async Task<IReadOnlyList<PaireTauxChangeDto>> ListerPairesSupporteesAsync(
        CancellationToken cancellationToken = default)
    {
        var paires = await ChargerPairesActivesAsync(cancellationToken);
        return paires
            .Select(p => new PaireTauxChangeDto(p.DeviseBase, p.DeviseQuote, p.Cle))
            .ToList();
    }

    public async Task<IReadOnlyList<TauxChangeDto>> ListAsync(
        TauxChangeListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ExigerLectureReferentiel();
        var rows = await _repository.ListAsync(query ?? new TauxChangeListQuery(), cancellationToken);
        var utilises = await _repository.GetIdsReferenceParProcedureAsync(
            rows.Select(r => r.IdTauxChange),
            cancellationToken);
        return rows.Select(r => Map(r, !utilises.Contains(r.IdTauxChange))).ToList();
    }

    public async Task<TauxChangeDto?> GetByIdAsync(long idTauxChange, CancellationToken cancellationToken = default)
    {
        ExigerLectureReferentiel();
        var row = await _repository.GetByIdAsync(idTauxChange, cancellationToken);
        if (row is null)
            return null;

        var utilise = await _repository.EstReferenceParProcedureAsync(idTauxChange, cancellationToken);
        return Map(row, !utilise);
    }

    public Task<TauxChangeApplicableDto?> GetApplicableVersUsdAsync(
        string deviseSource,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
        => GetApplicableAsync(deviseSource, TauxChangeConventions.DeviseUsd, dateReference, cancellationToken);

    public async Task<TauxChangeApplicableDto?> GetApplicableAsync(
        string deviseSource,
        string deviseCible,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        ExigerLectureOperationnelle();
        var source = NormaliserDevise(deviseSource, nameof(deviseSource));
        var cible = NormaliserDevise(deviseCible, nameof(deviseCible));

        if (TauxChangeConventions.EstIdentite(source, cible))
        {
            return new TauxChangeApplicableDto(
                null,
                source,
                cible,
                1m,
                1m,
                dateReference,
                StatutTauxChange.Actif,
                EstIdentite: true,
                EstInverseCalcule: false);
        }

        var paires = await ChargerPairesActivesAsync(cancellationToken);
        if (!TauxChangeConventions.TryResoudrePaire(paires, source, cible, out var paire))
            return null;

        var row = await _repository.FindApplicableCanoniqueAsync(
            paire.DeviseBase,
            paire.DeviseQuote,
            dateReference,
            cancellationToken);

        if (row is null)
            return null;

        ValiderLigneApplicable(row, paire, dateReference);
        return MapApplicable(row, paire, source, cible);
    }

    public ConversionUsdResultDto ConvertirVersUsd(
        decimal montantBrut,
        string deviseSource,
        decimal tauxReference,
        decimal tauxDirectionnel,
        long? idTauxChange,
        bool estIdentite,
        TauxChangeConventions.PaireCanonique paire)
    {
        var source = NormaliserDevise(deviseSource, nameof(deviseSource));
        var montantUsd = estIdentite
            ? montantBrut
            : TauxChangeConventions.ConvertirVersUsd(montantBrut, source, tauxReference, paire);

        return new ConversionUsdResultDto(
            montantBrut,
            source,
            tauxReference,
            tauxReference,
            montantUsd,
            idTauxChange,
            estIdentite);
    }

    public async Task<ConversionUsdResultDto> ConvertirVersUsdAsync(
        decimal montantBrut,
        string deviseSource,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        var applicable = await GetApplicableVersUsdAsync(deviseSource, dateReference, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Aucun taux de change applicable pour {deviseSource.Trim().ToUpperInvariant()} à la date {dateReference:yyyy-MM-dd}.");

        if (applicable.EstIdentite)
        {
            var source = NormaliserDevise(applicable.DeviseSource, nameof(deviseSource));
            return new ConversionUsdResultDto(
                montantBrut,
                source,
                1m,
                1m,
                montantBrut,
                applicable.IdTauxChange,
                true);
        }

        var paires = await ChargerPairesActivesAsync(cancellationToken);
        if (!TauxChangeConventions.TryResoudrePaire(
                paires,
                applicable.DeviseSource,
                TauxChangeConventions.DeviseUsd,
                out var paire))
        {
            throw new InvalidOperationException(
                $"Paire de change non configurée pour {applicable.DeviseSource}/USD.");
        }

        return ConvertirVersUsd(
            montantBrut,
            applicable.DeviseSource,
            applicable.TauxReference,
            applicable.Taux,
            applicable.IdTauxChange,
            applicable.EstIdentite,
            paire);
    }

    public ConversionResultDto Convertir(
        decimal montantSource,
        string deviseSource,
        string deviseCible,
        decimal tauxReference,
        decimal tauxDirectionnel,
        long? idTauxChange,
        bool estIdentite,
        TauxChangeConventions.PaireCanonique paire)
    {
        var source = NormaliserDevise(deviseSource, nameof(deviseSource));
        var cible = NormaliserDevise(deviseCible, nameof(deviseCible));

        var montantCible = estIdentite
            ? montantSource
            : decimal.Round(
                TauxChangeConventions.Convertir(montantSource, source, cible, tauxReference, paire),
                4,
                MidpointRounding.AwayFromZero);

        return new ConversionResultDto(
            montantSource,
            source,
            montantCible,
            cible,
            tauxDirectionnel,
            tauxReference,
            idTauxChange,
            estIdentite);
    }

    public async Task<ConversionResultDto> ConvertirAsync(
        decimal montantSource,
        string deviseSource,
        string deviseCible,
        DateOnly dateReference,
        CancellationToken cancellationToken = default)
    {
        ExigerLectureOperationnelle();
        var source = NormaliserDevise(deviseSource, nameof(deviseSource));
        var cible = NormaliserDevise(deviseCible, nameof(deviseCible));

        if (TauxChangeConventions.EstIdentite(source, cible))
        {
            var paireIdentite = new TauxChangeConventions.PaireCanonique(source, cible);
            return Convertir(montantSource, source, cible, 1m, 1m, null, estIdentite: true, paireIdentite);
        }

        var paires = await ChargerPairesActivesAsync(cancellationToken);
        if (!TauxChangeConventions.TryResoudrePaire(paires, source, cible, out var paire))
        {
            throw new InvalidOperationException(
                $"Aucun taux de change actif applicable pour {source}/{cible} à la date {dateReference:yyyy-MM-dd}.");
        }

        var applicable = await ResoudreApplicableCanoniqueAsync(paire, source, cible, dateReference, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Aucun taux de change actif applicable pour {source}/{cible} à la date {dateReference:yyyy-MM-dd}.");

        return Convertir(
            montantSource,
            source,
            cible,
            applicable.TauxReference,
            applicable.Taux,
            applicable.IdTauxChange,
            applicable.EstIdentite,
            paire);
    }

    public async Task<TauxChangeDto> CreateVersionAsync(
        CreateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var userId = _currentUser.RequireUserId();
        var deviseBase = NormaliserDevise(request.DeviseBase, nameof(request.DeviseBase));
        var deviseQuote = NormaliserDevise(request.DeviseQuote, nameof(request.DeviseQuote));

        if (request.TauxReference <= 0m)
            throw new ArgumentException("Le taux de référence doit être strictement positif.");

        if (TauxChangeConventions.EstIdentite(deviseBase, deviseQuote))
            throw new ArgumentException("La devise de base et la devise cotée doivent être différentes.");

        // Conserve l'orientation canonique du registre ; si l'utilisateur saisit l'inverse,
        // on stocke 1/taux dans le sens canonique (l'affichage / conversion recalcule l'inverse).
        var paire = await _paires.CreerOuObtenirPaireCanoniqueAsync(deviseBase, deviseQuote, cancellationToken);
        var orientationInverse = !string.Equals(paire.DeviseBase, deviseBase, StringComparison.Ordinal)
            || !string.Equals(paire.DeviseQuote, deviseQuote, StringComparison.Ordinal);
        var tauxCanonique = orientationInverse
            ? 1m / request.TauxReference
            : request.TauxReference;
        var stockBase = paire.DeviseBase;
        var stockQuote = paire.DeviseQuote;

        var actifCourant = await _repository.FindActifCourantCanoniqueAsync(
            stockBase, stockQuote, cancellationToken);

        // Nouvelle version « libre » : date d'effet strictement postérieure, sans collision de date.
        if (actifCourant is null
            || (request.DateEffet > actifCourant.DateEffet
                && !await _repository.ExistsForCanoniqueAndDateEffetAsync(
                    stockBase, stockQuote, request.DateEffet, null, cancellationToken)))
        {
            var entityLibre = new TauxChange
            {
                DeviseSource = stockBase,
                DeviseCible = stockQuote,
                Taux = tauxCanonique,
                DateEffet = request.DateEffet,
                Statut = StatutTauxChange.Actif,
                FK_UtilisateurCreation = userId,
                DateCreation = DateTime.Now,
            };

            var createdLibre = await _repository.CreateVersionReplacingActifAsync(
                entityLibre, userId, cancellationToken);
            var detailLibre = await _repository.GetByIdAsync(createdLibre.IdTauxChange, cancellationToken)
                ?? createdLibre;
            return Map(detailLibre, estModifiable: true);
        }

        // Un ACTIF existe déjà pour cette paire (même date, date antérieure, ou collision).
        var utilise = await _repository.EstReferenceParProcedureAsync(
            actifCourant.IdTauxChange, cancellationToken);
        var modePropose = utilise ? "CLOTURER_ET_CREER" : "ECRASER";

        if (!request.ConfirmerRemplacement)
        {
            throw new TauxChangeRemplacementRequisException(
                new TauxChangeRemplacementProposeDto(
                    Code: "TAUX_REMPLACEMENT_REQUIS",
                    Message:
                        $"Un taux ACTIF existe déjà pour {stockBase}/{stockQuote} "
                        + $"(1 {stockBase} = {actifCourant.Taux:0.########} {stockQuote}, "
                        + $"effet {actifCourant.DateEffet:dd/MM/yyyy}). "
                        + (utilise
                            ? "Il a déjà été utilisé : confirmation pour le clôturer et enregistrer le nouveau."
                            : "Il n'a pas encore été utilisé : confirmation pour l'écraser par le nouveau taux."),
                    IdTauxExistant: actifCourant.IdTauxChange,
                    DeviseBase: stockBase,
                    DeviseQuote: stockQuote,
                    TauxReferenceExistant: actifCourant.Taux,
                    DateEffetExistante: actifCourant.DateEffet,
                    EstUtilise: utilise,
                    ModePropose: modePropose));
        }

        if (!utilise)
        {
            var tracked = await _repository.GetByIdTrackedAsync(actifCourant.IdTauxChange, cancellationToken)
                ?? throw new InvalidOperationException("Taux de change introuvable.");
            tracked.Taux = tauxCanonique;
            tracked.DateEffet = request.DateEffet;
            tracked.FK_UtilisateurModification = userId;
            tracked.DateModification = DateTime.Now;
            await _repository.SaveChangesAsync(cancellationToken);
            var detailEcrase = await _repository.GetByIdAsync(tracked.IdTauxChange, cancellationToken)
                ?? tracked;
            return Map(detailEcrase, estModifiable: true);
        }

        var entity = new TauxChange
        {
            DeviseSource = stockBase,
            DeviseCible = stockQuote,
            Taux = tauxCanonique,
            DateEffet = request.DateEffet,
            Statut = StatutTauxChange.Actif,
            FK_UtilisateurCreation = userId,
            DateCreation = DateTime.Now,
        };

        var created = await _repository.CreateVersionReplacingActifAsync(entity, userId, cancellationToken);
        var detail = await _repository.GetByIdAsync(created.IdTauxChange, cancellationToken)
            ?? created;
        return Map(detail, estModifiable: true);
    }

    public async Task<TauxChangeDto> UpdateVersionAsync(
        long idTauxChange,
        UpdateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var userId = _currentUser.RequireUserId();

        if (request.TauxReference <= 0m)
            throw new ArgumentException("Le taux de référence doit être strictement positif.");

        var entity = await _repository.GetByIdTrackedAsync(idTauxChange, cancellationToken)
            ?? throw new InvalidOperationException("Taux de change introuvable.");

        if (await _repository.EstReferenceParProcedureAsync(idTauxChange, cancellationToken))
        {
            throw new InvalidOperationException(
                "Ce taux a déjà été appliqué par une procédure (demande de paiement) et ne peut plus être modifié.");
        }

        if (request.DateEffet != entity.DateEffet
            && await _repository.ExistsForCanoniqueAndDateEffetAsync(
                entity.DeviseSource,
                entity.DeviseCible,
                request.DateEffet,
                idTauxChange,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"Un taux existe déjà pour {entity.DeviseSource}/{entity.DeviseCible} "
                + $"à la date {request.DateEffet:yyyy-MM-dd}.");
        }

        entity.Taux = request.TauxReference;
        entity.DateEffet = request.DateEffet;
        entity.FK_UtilisateurModification = userId;
        entity.DateModification = DateTime.Now;

        await _repository.SaveChangesAsync(cancellationToken);

        var detail = await _repository.GetByIdAsync(idTauxChange, cancellationToken)
            ?? entity;
        return Map(detail, estModifiable: true);
    }

    public async Task<TauxChangeDto> InactivateAsync(
        long idTauxChange,
        InactivateTauxChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var userId = _currentUser.RequireUserId();
        var entity = await _repository.GetByIdTrackedAsync(idTauxChange, cancellationToken)
            ?? throw new InvalidOperationException("Taux de change introuvable.");

        if (!string.Equals(StatutTauxChange.Normaliser(entity.Statut), StatutTauxChange.Actif, StringComparison.Ordinal))
            throw new InvalidOperationException("Seul un taux actif peut être désactivé.");

        var actifCourant = await _repository.FindActifCourantCanoniqueAsync(
            entity.DeviseSource,
            entity.DeviseCible,
            cancellationToken);

        if (actifCourant is not null && actifCourant.IdTauxChange == entity.IdTauxChange)
        {
            throw new InvalidOperationException(
                "La désactivation manuelle de la version courante n'est pas autorisée. "
                + "Créez une nouvelle version pour mettre à jour le taux.");
        }

        entity.Statut = StatutTauxChange.Inactif;
        entity.FK_UtilisateurModification = userId;
        entity.DateModification = DateTime.Now;
        _ = request;

        await _repository.SaveChangesAsync(cancellationToken);

        var detail = await _repository.GetByIdAsync(idTauxChange, cancellationToken)
            ?? entity;
        var utilise = await _repository.EstReferenceParProcedureAsync(idTauxChange, cancellationToken);
        return Map(detail, !utilise);
    }

    private async Task<IReadOnlyList<TauxChangeConventions.PaireCanonique>> ChargerPairesActivesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await _paires.ListActivesAsync(cancellationToken);
        return rows
            .Select(p => new TauxChangeConventions.PaireCanonique(p.DeviseBase, p.DeviseQuote))
            .ToList();
    }

    private async Task<TauxChangeApplicableDto?> ResoudreApplicableCanoniqueAsync(
        TauxChangeConventions.PaireCanonique paire,
        string sourceDemandee,
        string cibleDemandee,
        DateOnly dateReference,
        CancellationToken cancellationToken)
    {
        var row = await _repository.FindApplicableCanoniqueAsync(
            paire.DeviseBase,
            paire.DeviseQuote,
            dateReference,
            cancellationToken);

        if (row is null)
            return null;

        ValiderLigneApplicable(row, paire, dateReference);
        return MapApplicable(row, paire, sourceDemandee, cibleDemandee);
    }

    private static void ValiderLigneApplicable(
        TauxChange row,
        TauxChangeConventions.PaireCanonique paire,
        DateOnly dateReference)
    {
        if (row.DateEffet > dateReference)
        {
            throw new InvalidOperationException(
                $"Aucun taux applicable pour {paire.Cle} à la date {dateReference:yyyy-MM-dd}.");
        }
    }

    private static TauxChangeApplicableDto MapApplicable(
        TauxChange row,
        TauxChangeConventions.PaireCanonique paire,
        string sourceDemandee,
        string cibleDemandee)
    {
        var tauxRef = row.Taux;
        var tauxDirectionnel = TauxChangeConventions.CalculerTauxDirectionnel(
            tauxRef,
            sourceDemandee,
            cibleDemandee,
            paire);

        var estInverse = sourceDemandee == paire.DeviseQuote && cibleDemandee == paire.DeviseBase;

        return new TauxChangeApplicableDto(
            row.IdTauxChange,
            sourceDemandee,
            cibleDemandee,
            tauxDirectionnel,
            tauxRef,
            row.DateEffet,
            StatutTauxChange.Normaliser(row.Statut),
            EstIdentite: false,
            EstInverseCalcule: estInverse);
    }

    private static TauxChangeDto Map(TauxChange t, bool estModifiable)
        => new(
            t.IdTauxChange,
            t.DeviseSource,
            t.DeviseCible,
            t.Taux,
            t.DateEffet,
            StatutTauxChange.Normaliser(t.Statut),
            t.DateCreation,
            t.FK_UtilisateurCreation,
            t.UtilisateurCreation?.NomUtilisateur,
            t.DateModification,
            t.FK_UtilisateurModification,
            t.UtilisateurModification?.NomUtilisateur,
            estModifiable);

    private static string NormaliserDevise(string devise, string paramName)
    {
        DemandePaiementMontants.ValiderDevise(devise);
        return DemandePaiementMontants.NormaliserCodeDevise(devise);
    }

    private void ExigerLectureReferentiel()
    {
        if (_currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : referentiels.ecrire.");
    }

    private void ExigerLectureOperationnelle()
    {
        if (_currentUser.HasPermission(AppPermissions.PaiementsLire)
            || _currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
            || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
            || _currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : paiements.lire ou referentiels.ecrire.");
    }

    private void ExigerEcriture()
    {
        if (_currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : referentiels.ecrire.");
    }
}
