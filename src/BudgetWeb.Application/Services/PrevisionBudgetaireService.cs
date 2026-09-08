using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public class PrevisionBudgetaireService : IPrevisionBudgetaireService
{
    private readonly IPrevisionBudgetaireRepository _repository;
    private readonly IWorkflowPrevisionUbService _workflowUbService;
    private readonly IClassementAeService _classementAe;
    private readonly IPerimetreAccesService _perimetreAcces;

    public PrevisionBudgetaireService(
        IPrevisionBudgetaireRepository repository,
        IWorkflowPrevisionUbService workflowUbService,
        IClassementAeService classementAe,
        IPerimetreAccesService perimetreAcces)
    {
        _repository = repository;
        _workflowUbService = workflowUbService;
        _classementAe = classementAe;
        _perimetreAcces = perimetreAcces;
    }

    public async Task<IReadOnlyList<PrevisionBudgetaireDto>> GetByFiltresAsync(
        long? idVersion,
        long? idTypeBudget,
        long? idUB,
        long? idModePrevision,
        string? libelleItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default)
    {
        if (idUB is > 0)
            await _perimetreAcces.GarantirAccesUbLecturePrevisionsAsync(idUB.Value, cancellationToken);

        var items = await _repository.GetByFiltresAsync(
            idVersion, idTypeBudget, idUB, idModePrevision, libelleItemAE, idItemBI, cancellationToken);

        if (idUB is > 0 || _perimetreAcces.PeutVoirToutesUbPrevisions())
            return items;

        var filtered = new List<PrevisionBudgetaireDto>();
        foreach (var item in items)
        {
            if (await _perimetreAcces.PeutAccederUbLectureCourantAsync(item.IdUB, cancellationToken))
                filtered.Add(item);
        }

        return filtered;
    }

    public async Task<PrevisionBudgetaireDto?> GetByIdAsync(long idPrevision, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(idPrevision, cancellationToken);
        if (item is null)
            return null;

        await _perimetreAcces.GarantirAccesUbLecturePrevisionsAsync(item.IdUB, cancellationToken);
        return item;
    }

    public Task<IReadOnlyList<PrevisionResumeCategorieDto>> GetResumeAsync(
        long idVersion,
        CancellationToken cancellationToken = default)
        => _repository.GetResumeParTypeAsync(idVersion, cancellationToken);

    public Task<IReadOnlyList<string>> ListLibellesItemAEAsync(
        long? idVersion,
        long? idExercice,
        CancellationToken cancellationToken = default)
        => _repository.ListLibellesItemAEAsync(idVersion, idExercice, cancellationToken);

    public async Task<PrevisionGrilleDto> GetGrilleAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        string? libelleItemAE,
        long? idGroupeItemAE,
        long? idItemBI,
        CancellationToken cancellationToken = default)
    {
        var (_, versionExists) = await _repository.GetVersionStatutAsync(idVersion, cancellationToken);
        if (!versionExists)
        {
            throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");
        }

        // Statut opérationnel = WORKFLOW Version×UB (pas l'agrégat VERSION).
        var statutUb = await _workflowUbService.GetStatutOperationnelAsync(idVersion, idUB, cancellationToken);

        var (codeType, _, typeExists, typeActif) = await _repository.GetTypeBudgetAsync(idTypeBudget, cancellationToken);
        if (!typeExists)
        {
            throw new InvalidOperationException("Le type de budget indiqué n'existe pas.");
        }

        if (!typeActif)
        {
            throw new InvalidOperationException("Le type de budget indiqué est inactif.");
        }

        var (codeMode, _, modeExists, modeActif) = await _repository.GetModePrevisionAsync(idModePrevision, cancellationToken);
        if (!modeExists)
        {
            throw new InvalidOperationException("Le mode de prévision indiqué n'existe pas.");
        }

        if (!modeActif)
        {
            throw new InvalidOperationException("Le mode de prévision indiqué est inactif.");
        }

        if (!await _repository.ExistsUBAsync(idUB, cancellationToken))
        {
            throw new InvalidOperationException("L'unité budgétaire indiquée n'existe pas.");
        }

        await _perimetreAcces.GarantirAccesUbLecturePrevisionsAsync(idUB, cancellationToken);

        var ubInfo = await _repository.GetUBInfoAsync(idUB, cancellationToken);
        var codeTypeNorm = codeType.Trim().ToUpperInvariant();
        var actionAE = NormaliserTexteOptionnel(libelleItemAE);

        if (codeTypeNorm == TypeBudgetCode.ActionsExploitation && actionAE is null)
        {
            throw new InvalidOperationException(
                "L'action d'exploitation (Item AE) est obligatoire pour construire la grille AE.");
        }

        if (codeTypeNorm == TypeBudgetCode.BudgetInvestissement)
        {
            if (idItemBI is null or <= 0)
            {
                throw new InvalidOperationException("L'item BI est obligatoire pour construire la grille BI.");
            }

            if (!await _repository.ExistsItemBIAsync(idItemBI.Value, cancellationToken))
            {
                throw new InvalidOperationException("L'item BI indiqué n'existe pas.");
            }
        }

        var codeModeNorm = codeMode.Trim().ToUpperInvariant();
        // ANNUEL : pas besoin des 12 mois pour l'affichage / édition annuelle.
        var includeRepartitions = codeModeNorm != ModePrevisionCode.Annuel;

        var previsions = await _repository.GetForGrilleAsync(
            idVersion,
            idTypeBudget,
            idUB,
            codeTypeNorm == TypeBudgetCode.ActionsExploitation ? actionAE : null,
            codeTypeNorm == TypeBudgetCode.BudgetInvestissement ? idItemBI : null,
            includeRepartitions,
            cancellationToken);

        var lignes = codeTypeNorm switch
        {
            TypeBudgetCode.DepensesCourantes or TypeBudgetCode.ActionsExploitation
                => await ConstruireLignesRbAsync(previsions, cancellationToken),
            TypeBudgetCode.BudgetInvestissement
                => ConstruireLignesBi(previsions),
            _ => throw new InvalidOperationException($"Type de budget non supporté : {codeType}.")
        };

        return new PrevisionGrilleDto(
            idVersion,
            statutUb,
            EstModificationAutorisee(statutUb),
            idTypeBudget,
            codeTypeNorm,
            idModePrevision,
            codeModeNorm,
            idUB,
            ubInfo?.CodeUB ?? string.Empty,
            idItemBI,
            actionAE,
            idGroupeItemAE,
            lignes);
    }

    public async Task<PrevisionGrillePageDto> GetGrillePageAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        string? libelleItemAE,
        long? idGroupeItemAE,
        long? idItemBI,
        int page,
        int pageSize,
        string? search,
        string? filtre,
        long? idGroupeRB = null,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 100,
            > 500 => 500,
            _ => pageSize
        };

        var full = await GetGrilleAsync(
            idVersion, idTypeBudget, idModePrevision, idUB, libelleItemAE, idGroupeItemAE, idItemBI, cancellationToken);

        IEnumerable<PrevisionGrilleLigneDto> query = full.Lignes;

        // Filtre affichage par Groupe N1 (idGroupeRB, pas codeGroupe — 02 A ≠ 02 B).
        if (idGroupeRB is long idGroupeFiltre)
        {
            query = query.Where(l => l.IdGroupeRB == idGroupeFiltre);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var all = query.ToList();

            var groupesParNom = new HashSet<long>();
            var groupesContexte = new HashSet<long>();
            var keepRbIds = new HashSet<long>();
            var keepBiDetails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ligne in all)
            {
                var match =
                    (ligne.CodeRB?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (ligne.LibelleRB?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (ligne.CodeGroupe?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (ligne.LibelleGroupe?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (ligne.DetailBI?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

                if (!match)
                {
                    continue;
                }

                if (ligne.EstSection && ligne.IdGroupeRB is long idGroupeHeader)
                {
                    groupesParNom.Add(idGroupeHeader);
                    continue;
                }

                if (ligne.IdRB is long idRb)
                {
                    keepRbIds.Add(idRb);
                    if (ligne.IdGroupeRB is long idGroupe)
                    {
                        groupesContexte.Add(idGroupe);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(ligne.DetailBI))
                {
                    keepBiDetails.Add(ligne.DetailBI);
                }
            }

            query = all.Where(l =>
                (l.EstSection
                    && l.IdGroupeRB is long ig
                    && (groupesParNom.Contains(ig) || groupesContexte.Contains(ig)))
                || (l.IdRB is long id && keepRbIds.Contains(id))
                || (!l.EstSection
                    && l.IdGroupeRB is long igLeaf
                    && groupesParNom.Contains(igLeaf))
                || (l.IdRB is null
                    && !string.IsNullOrWhiteSpace(l.DetailBI)
                    && keepBiDetails.Contains(l.DetailBI)));
        }

        var filtreNorm = (filtre ?? "toutes").Trim().ToLowerInvariant();
        query = filtreNorm switch
        {
            "avec" => query.Where(l =>
                l.EstSection
                || l.IdPrevision is not null
                || l.MontantAnnuel != 0
                || l.CumulMensuel != 0
                || l.Repartitions.Any(r => r.Montant != 0)),
            "sans" => query.Where(l =>
                l.EstSection
                || (l.IdPrevision is null
                    && l.MontantAnnuel == 0
                    && l.CumulMensuel == 0
                    && l.Repartitions.All(r => r.Montant == 0))),
            _ => query
        };

        var filtered = query.ToList();

        // Masquer les en-têtes de groupe sans feuille restante (filtres avec/sans).
        if (filtreNorm is "avec" or "sans")
        {
            var groupesAvecFeuilles = filtered
                .Where(l => !l.EstSection && l.IdGroupeRB is not null)
                .Select(l => l.IdGroupeRB!.Value)
                .ToHashSet();
            filtered = filtered
                .Where(l =>
                    !l.EstSection
                    || (l.IdGroupeRB is long ig && groupesAvecFeuilles.Contains(ig)))
                .ToList();
        }

        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PrevisionGrillePageDto(
            full.IdVersion,
            full.StatutVersion,
            full.ModificationAutorisee,
            full.IdTypeBudget,
            full.CodeType,
            full.IdModePrevision,
            full.CodeMode,
            full.IdUB,
            full.CodeUB,
            full.IdItemBI,
            full.LibelleItemAE,
            full.IdGroupeItemAE,
            page,
            pageSize,
            total,
            page * pageSize < total,
            items);
    }

    public async Task<PrevisionBudgetaireDto> CreateAsync(
        CreatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var contexte = await ValiderContexteAsync(
            request.IdVersion,
            request.IdTypeBudget,
            request.IdModePrevision,
            request.IdUB,
            request.IdRB,
            request.IdItemBI,
            request.IdGroupeItemAE,
            request.LibelleItemAE,
            request.DetailBI,
            request.IdUtilisateurCreation,
            exigerModificationVersion: true,
            cancellationToken);

        var (montant, repartitions) = NormaliserMontants(
            contexte.CodeMode,
            request.MontantAnnuel,
            request.Repartitions);

        if (contexte.CodeType == TypeBudgetCode.ActionsExploitation
            && !string.IsNullOrWhiteSpace(contexte.LibelleItemAE))
        {
            await _classementAe.GarantirGroupeHomogeneAsync(
                request.IdVersion,
                request.IdUB,
                contexte.LibelleItemAE!,
                contexte.IdGroupeItemAE,
                cancellationToken);
        }

        var created = await _repository.ExecuteInTransactionAsync(async ct =>
            await _repository.CreateAsync(
                request.IdVersion,
                request.IdTypeBudget,
                request.IdModePrevision,
                request.IdUB,
                contexte.IdRB,
                contexte.IdItemBI,
                contexte.IdGroupeItemAE,
                contexte.LibelleItemAE,
                contexte.DetailBI,
                montant,
                request.IdUtilisateurCreation,
                repartitions,
                ct), cancellationToken);

        if (contexte.CodeType == TypeBudgetCode.ActionsExploitation
            && !string.IsNullOrWhiteSpace(contexte.LibelleItemAE))
        {
            await _classementAe.ApresEcritureAeAsync(
                request.IdVersion,
                request.IdUB,
                contexte.LibelleItemAE!,
                contexte.IdGroupeItemAE,
                cancellationToken);
        }

        await ApresSaisieUbAsync(
            request.IdVersion,
            request.IdUB,
            request.IdUtilisateurCreation,
            cancellationToken);

        return created;
    }

    public async Task<PrevisionBudgetaireDto?> UpdateAsync(
        long idPrevision,
        UpdatePrevisionBudgetaireRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idPrevision, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        await GarantirModificationAutoriseeUbAsync(existing.IdVersion, existing.IdUB, cancellationToken);

        if (!await _repository.ExistsUtilisateurAsync(request.IdUtilisateurModification, cancellationToken))
        {
            throw new InvalidOperationException(
                "L'utilisateur de modification indiqué n'existe pas dans le référentiel UTILISATEUR.");
        }

        var detailBI = existing.CodeType == TypeBudgetCode.BudgetInvestissement
            ? NormaliserTexteObligatoire(request.DetailBI ?? existing.DetailBI, "Le détail BI")
            : null;
        var libelleItemAE = existing.CodeType == TypeBudgetCode.ActionsExploitation
            ? NormaliserTexteObligatoire(request.LibelleItemAE ?? existing.LibelleItemAE, "L'action d'exploitation")
            : null;
        var idGroupe = existing.CodeType == TypeBudgetCode.ActionsExploitation
            ? (request.IdGroupeItemAE ?? existing.IdGroupeItemAE)
            : null;

        if (idGroupe is > 0 && !await _repository.ExistsGroupeItemAEAsync(idGroupe.Value, cancellationToken))
        {
            throw new InvalidOperationException("Le groupe AE indiqué n'existe pas.");
        }

        if (idGroupe is <= 0)
        {
            idGroupe = null;
        }

        var remplacerRepartitions = request.Repartitions is not null
            || existing.CodeMode == ModePrevisionCode.Mensuel;
        var (montant, repartitions) = NormaliserMontants(
            existing.CodeMode,
            request.MontantAnnuel ?? existing.MontantAnnuel,
            request.Repartitions ?? existing.Repartitions);

        if (existing.CodeType == TypeBudgetCode.ActionsExploitation
            && !string.IsNullOrWhiteSpace(libelleItemAE))
        {
            var ancienLibelle = existing.LibelleItemAE ?? libelleItemAE;
            var libelleChange = !string.Equals(
                NormaliserTexteOptionnel(ancienLibelle),
                NormaliserTexteOptionnel(libelleItemAE),
                StringComparison.Ordinal);
            var groupeChange = (existing.IdGroupeItemAE ?? 0) != (idGroupe ?? 0);

            // Homogénéité : seulement si on ne change pas volontairement le groupe de l'action.
            if (!libelleChange && !groupeChange)
            {
                await _classementAe.GarantirGroupeHomogeneAsync(
                    existing.IdVersion,
                    existing.IdUB,
                    libelleItemAE!,
                    idGroupe,
                    cancellationToken);
            }

            var updated = await _repository.ExecuteInTransactionAsync(async ct =>
                await _repository.UpdateAsync(
                    idPrevision,
                    idGroupe,
                    libelleItemAE,
                    detailBI,
                    montant,
                    request.IdUtilisateurModification,
                    remplacerRepartitions ? repartitions : null,
                    remplacerRepartitions,
                    ct), cancellationToken);

            if (libelleChange)
            {
                await _classementAe.ApresRenommageAeAsync(
                    existing.IdVersion,
                    existing.IdUB,
                    ancienLibelle!,
                    libelleItemAE!,
                    idGroupe,
                    cancellationToken);
            }
            else if (groupeChange)
            {
                await _classementAe.ApresChangementGroupeAeAsync(
                    existing.IdVersion,
                    existing.IdUB,
                    libelleItemAE!,
                    idGroupe,
                    cancellationToken);
            }
            else
            {
                await _classementAe.ApresEcritureAeAsync(
                    existing.IdVersion,
                    existing.IdUB,
                    libelleItemAE!,
                    idGroupe,
                    cancellationToken);
            }

            await ApresSaisieUbAsync(
                existing.IdVersion,
                existing.IdUB,
                request.IdUtilisateurModification,
                cancellationToken);

            return updated;
        }

        var updatedNonAe = await _repository.ExecuteInTransactionAsync(async ct =>
            await _repository.UpdateAsync(
                idPrevision,
                idGroupe,
                libelleItemAE,
                detailBI,
                montant,
                request.IdUtilisateurModification,
                remplacerRepartitions ? repartitions : null,
                remplacerRepartitions,
                ct), cancellationToken);

        await ApresSaisieUbAsync(
            existing.IdVersion,
            existing.IdUB,
            request.IdUtilisateurModification,
            cancellationToken);

        return updatedNonAe;
    }

    public async Task<bool> DeleteAsync(long idPrevision, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idPrevision, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await GarantirModificationAutoriseeUbAsync(existing.IdVersion, existing.IdUB, cancellationToken);
        var libelleAe = existing.CodeType == TypeBudgetCode.ActionsExploitation ? existing.LibelleItemAE : null;
        var deleted = await _repository.DeleteAsync(idPrevision, cancellationToken);
        if (deleted && libelleAe is not null)
        {
            await _classementAe.ApresSuppressionAeAsync(
                existing.IdVersion, existing.IdUB, libelleAe, cancellationToken);
        }

        return deleted;
    }

    public async Task<SauvegarderGrillePrevisionResultDto> SauvegarderGrilleAsync(
        SauvegarderGrillePrevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _repository.ExistsUtilisateurAsync(request.IdUtilisateur, cancellationToken))
        {
            throw new InvalidOperationException(
                "L'utilisateur indiqué n'existe pas dans le référentiel UTILISATEUR. " +
                "La sauvegarde d'une prévision exige FK_UtilisateurCreation.");
        }

        var contexte = await ValiderContexteAsync(
            request.IdVersion,
            request.IdTypeBudget,
            request.IdModePrevision,
            request.IdUB,
            idRB: null,
            request.IdItemBI,
            request.IdGroupeItemAE,
            request.LibelleItemAE,
            detailBI: null,
            request.IdUtilisateur,
            exigerModificationVersion: true,
            cancellationToken,
            validerChampsLigne: false);

        var creees = 0;
        var modifiees = 0;
        var supprimees = 0;
        var actionsSupprimees = new HashSet<string>(StringComparer.Ordinal);

        if (contexte.CodeType == TypeBudgetCode.ActionsExploitation
            && !string.IsNullOrWhiteSpace(contexte.LibelleItemAE))
        {
            await _classementAe.GarantirGroupeHomogeneAsync(
                request.IdVersion,
                request.IdUB,
                contexte.LibelleItemAE!,
                contexte.IdGroupeItemAE,
                cancellationToken);
        }

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var ligne in request.Lignes)
            {
                if (ligne.Supprimer)
                {
                    if (ligne.IdPrevision is long idDel)
                    {
                        var existingDel = await _repository.GetByIdAsync(idDel, ct)
                            ?? throw new InvalidOperationException($"Prévision {idDel} introuvable.");
                        VerifierAppartenanceContexte(existingDel, request, contexte.CodeType);
                        if (existingDel.CodeType == TypeBudgetCode.ActionsExploitation
                            && !string.IsNullOrWhiteSpace(existingDel.LibelleItemAE))
                        {
                            actionsSupprimees.Add(existingDel.LibelleItemAE!);
                        }

                        if (await _repository.DeleteAsync(idDel, ct))
                        {
                            supprimees++;
                        }
                    }

                    continue;
                }

                var (montant, repartitions) = NormaliserMontants(
                    contexte.CodeMode,
                    ligne.MontantAnnuel,
                    ligne.Repartitions);

                var estMensuel = contexte.CodeMode.Equals(
                    ModePrevisionCode.Mensuel,
                    StringComparison.OrdinalIgnoreCase);
                // ANNUEL : ne pas toucher aux répartitions existantes.
                // MENSUEL : remplacer (y compris liste vide = répartition non définie).
                var remplacerReps = estMensuel;
                var repsPourUpdate = estMensuel ? repartitions : null;

                if (ligne.IdPrevision is long idExist)
                {
                    var existing = await _repository.GetByIdAsync(idExist, ct)
                        ?? throw new InvalidOperationException($"Prévision {idExist} introuvable.");
                    VerifierAppartenanceContexte(existing, request, contexte.CodeType);

                    var detail = contexte.CodeType == TypeBudgetCode.BudgetInvestissement
                        ? NormaliserTexteObligatoire(ligne.DetailBI ?? existing.DetailBI, "Le détail BI")
                        : null;

                    await _repository.UpdateAsync(
                        idExist,
                        contexte.IdGroupeItemAE,
                        contexte.LibelleItemAE,
                        detail,
                        montant,
                        request.IdUtilisateur,
                        repsPourUpdate,
                        remplacerRepartitions: remplacerReps,
                        ct);
                    modifiees++;
                    continue;
                }

                if (!LigneADuContenu(contexte.CodeMode, montant, repartitions, ligne.DetailBI))
                {
                    continue;
                }

                long? idRB = null;
                long? idItemBI = null;
                string? detailBI = null;

                if (contexte.CodeType is TypeBudgetCode.DepensesCourantes or TypeBudgetCode.ActionsExploitation)
                {
                    if (ligne.IdRB is null or <= 0)
                    {
                        throw new InvalidOperationException("Chaque ligne DC/AE doit indiquer une rubrique budgétaire.");
                    }

                    if (!await _repository.ExistsRBAsync(ligne.IdRB.Value, ct))
                    {
                        throw new InvalidOperationException($"La rubrique budgétaire {ligne.IdRB} n'existe pas.");
                    }

                    idRB = ligne.IdRB;
                }
                else
                {
                    idItemBI = contexte.IdItemBI;
                    detailBI = NormaliserTexteObligatoire(ligne.DetailBI, "Le détail BI");
                }

                // Upsert : une RB = une prévision (indépendamment du mode de vue).
                long? idExistant = contexte.CodeType switch
                {
                    TypeBudgetCode.DepensesCourantes when idRB is long rb
                        => await _repository.FindIdDcAsync(request.IdVersion, request.IdUB, rb, ct),
                    TypeBudgetCode.ActionsExploitation when idRB is long rb
                        && !string.IsNullOrWhiteSpace(contexte.LibelleItemAE)
                        => await _repository.FindIdAeAsync(
                            request.IdVersion, request.IdUB, rb, contexte.LibelleItemAE!, ct),
                    TypeBudgetCode.BudgetInvestissement when idItemBI is long item && detailBI is not null
                        => await _repository.FindIdBiAsync(request.IdVersion, request.IdUB, item, detailBI, ct),
                    _ => null
                };

                if (idExistant is long idUpd)
                {
                    await _repository.UpdateAsync(
                        idUpd,
                        contexte.IdGroupeItemAE,
                        contexte.LibelleItemAE,
                        detailBI,
                        montant,
                        request.IdUtilisateur,
                        repsPourUpdate,
                        remplacerRepartitions: remplacerReps,
                        ct);
                    modifiees++;
                    continue;
                }

                await _repository.CreateAsync(
                    request.IdVersion,
                    request.IdTypeBudget,
                    request.IdModePrevision,
                    request.IdUB,
                    idRB,
                    idItemBI,
                    contexte.IdGroupeItemAE,
                    contexte.LibelleItemAE,
                    detailBI,
                    montant,
                    request.IdUtilisateur,
                    // ANNUEL : jamais de répartition à la création. MENSUEL : seulement si définie.
                    estMensuel ? repartitions : [],
                    ct);
                creees++;
            }
        }, cancellationToken);

        if (contexte.CodeType == TypeBudgetCode.ActionsExploitation
            && !string.IsNullOrWhiteSpace(contexte.LibelleItemAE)
            && (creees > 0 || modifiees > 0))
        {
            await _classementAe.ApresEcritureAeAsync(
                request.IdVersion,
                request.IdUB,
                contexte.LibelleItemAE!,
                contexte.IdGroupeItemAE,
                cancellationToken);
        }

        foreach (var action in actionsSupprimees)
        {
            await _classementAe.ApresSuppressionAeAsync(
                request.IdVersion, request.IdUB, action, cancellationToken);
        }

        await ApresSaisieUbAsync(
            request.IdVersion,
            request.IdUB,
            request.IdUtilisateur,
            cancellationToken);

        var previsions = await _repository.GetByFiltresAsync(
            request.IdVersion,
            request.IdTypeBudget,
            request.IdUB,
            idModePrevision: null,
            contexte.LibelleItemAE,
            contexte.IdItemBI,
            cancellationToken);

        return new SauvegarderGrillePrevisionResultDto(creees, modifiees, supprimees, previsions);
    }

    private async Task<ContexteValide> ValiderContexteAsync(
        long idVersion,
        long idTypeBudget,
        long idModePrevision,
        long idUB,
        long? idRB,
        long? idItemBI,
        long? idGroupeItemAE,
        string? libelleItemAE,
        string? detailBI,
        long idUtilisateur,
        bool exigerModificationVersion,
        CancellationToken cancellationToken,
        bool validerChampsLigne = true)
    {
        var (_, versionExists) = await _repository.GetVersionStatutAsync(idVersion, cancellationToken);
        if (!versionExists)
        {
            throw new InvalidOperationException("La version budgétaire indiquée n'existe pas.");
        }

        if (exigerModificationVersion)
        {
            await GarantirModificationAutoriseeUbAsync(idVersion, idUB, cancellationToken);
        }

        var (codeType, _, typeExists, typeActif) = await _repository.GetTypeBudgetAsync(idTypeBudget, cancellationToken);
        if (!typeExists)
        {
            throw new InvalidOperationException("Le type de budget indiqué n'existe pas.");
        }

        if (!typeActif)
        {
            throw new InvalidOperationException("Le type de budget indiqué est inactif.");
        }

        var (codeMode, _, modeExists, modeActif) = await _repository.GetModePrevisionAsync(idModePrevision, cancellationToken);
        if (!modeExists)
        {
            throw new InvalidOperationException("Le mode de prévision indiqué n'existe pas.");
        }

        if (!modeActif)
        {
            throw new InvalidOperationException("Le mode de prévision indiqué est inactif.");
        }

        if (idUB <= 0 || !await _repository.ExistsUBAsync(idUB, cancellationToken))
        {
            throw new InvalidOperationException("L'unité budgétaire est obligatoire et doit exister.");
        }

        await _perimetreAcces.GarantirAccesUbSaisiePrevisionsAsync(idUB, cancellationToken);

        if (!await _repository.ExistsUtilisateurAsync(idUtilisateur, cancellationToken))
        {
            throw new InvalidOperationException(
                "L'utilisateur indiqué n'existe pas dans le référentiel UTILISATEUR. " +
                "La sauvegarde d'une prévision exige FK_UtilisateurCreation.");
        }

        var codeTypeNorm = codeType.Trim().ToUpperInvariant();
        var codeModeNorm = codeMode.Trim().ToUpperInvariant();
        if (codeModeNorm is not (ModePrevisionCode.Annuel or ModePrevisionCode.Mensuel))
        {
            throw new InvalidOperationException("Le mode de prévision doit être ANNUEL ou MENSUEL.");
        }

        long? rb = null;
        long? itemBI = null;
        long? groupe = null;
        string? actionAE = null;
        string? detail = null;

        switch (codeTypeNorm)
        {
            case TypeBudgetCode.DepensesCourantes:
                if (validerChampsLigne)
                {
                    if (idRB is null or <= 0)
                    {
                        throw new InvalidOperationException("La rubrique budgétaire est obligatoire pour une prévision DC.");
                    }

                    if (!await _repository.ExistsRBAsync(idRB.Value, cancellationToken))
                    {
                        throw new InvalidOperationException("La rubrique budgétaire indiquée n'existe pas.");
                    }

                    rb = idRB;
                }

                RefuseSiRenseigne(idItemBI, "Item BI", codeTypeNorm);
                RefuseSiRenseigne(idGroupeItemAE, "Groupe AE", codeTypeNorm);
                RefuseSiTexte(libelleItemAE, "Action d'exploitation", codeTypeNorm);
                RefuseSiTexte(detailBI, "Détail BI", codeTypeNorm);
                break;

            case TypeBudgetCode.ActionsExploitation:
                actionAE = NormaliserTexteObligatoire(libelleItemAE, "L'action d'exploitation");
                if (validerChampsLigne)
                {
                    if (idRB is null or <= 0)
                    {
                        throw new InvalidOperationException("La rubrique budgétaire est obligatoire pour une prévision AE.");
                    }

                    if (!await _repository.ExistsRBAsync(idRB.Value, cancellationToken))
                    {
                        throw new InvalidOperationException("La rubrique budgétaire indiquée n'existe pas.");
                    }

                    rb = idRB;
                }

                if (idGroupeItemAE is > 0)
                {
                    if (!await _repository.ExistsGroupeItemAEAsync(idGroupeItemAE.Value, cancellationToken))
                    {
                        throw new InvalidOperationException("Le groupe AE indiqué n'existe pas.");
                    }

                    groupe = idGroupeItemAE;
                }

                RefuseSiRenseigne(idItemBI, "Item BI", codeTypeNorm);
                RefuseSiTexte(detailBI, "Détail BI", codeTypeNorm);
                break;

            case TypeBudgetCode.BudgetInvestissement:
                if (idItemBI is null or <= 0 || !await _repository.ExistsItemBIAsync(idItemBI.Value, cancellationToken))
                {
                    throw new InvalidOperationException("L'item BI est obligatoire et doit exister pour une prévision BI.");
                }

                itemBI = idItemBI;
                if (validerChampsLigne)
                {
                    detail = NormaliserTexteObligatoire(detailBI, "Le détail BI");
                }

                RefuseSiRenseigne(idRB, "Rubrique budgétaire", codeTypeNorm);
                RefuseSiRenseigne(idGroupeItemAE, "Groupe AE", codeTypeNorm);
                RefuseSiTexte(libelleItemAE, "Action d'exploitation", codeTypeNorm);
                break;

            default:
                throw new InvalidOperationException($"Type de budget non supporté : {codeType}.");
        }

        return new ContexteValide(codeTypeNorm, codeModeNorm, rb, itemBI, groupe, actionAE, detail);
    }

    private async Task<IReadOnlyList<PrevisionGrilleLigneDto>> ConstruireLignesRbAsync(
        IReadOnlyList<PrevisionGrilleSourceDto> previsions,
        CancellationToken cancellationToken)
    {
        var rubriques = await _repository.GetRubriquesActivesAsync(cancellationToken);

        var byRb = previsions
            .Where(p => p.IdRB is not null)
            .GroupBy(p => p.IdRB!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Feuilles métier uniquement (rattachées à un groupe N1) — sections techniques exclues.
        var feuilles = rubriques
            .Where(r => r.IdGroupeRB is not null)
            .ToList();

        var groupesOrdonnes = feuilles
            .GroupBy(r => r.IdGroupeRB!.Value)
            .Select(g =>
            {
                var sample = g.First();
                return new
                {
                    IdGroupeRB = g.Key,
                    CodeGroupe = sample.CodeGroupe ?? string.Empty,
                    LibelleGroupe = sample.LibelleGroupe ?? string.Empty,
                    Ordre = sample.OrdreAffichageGroupe ?? int.MaxValue,
                    Rubriques = g.OrderBy(x => x.CodeRB, StringComparer.OrdinalIgnoreCase).ToList()
                };
            })
            .OrderBy(g => g.Ordre)
            .ThenBy(g => g.CodeGroupe, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.LibelleGroupe, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<PrevisionGrilleLigneDto>();

        foreach (var groupe in groupesOrdonnes)
        {
            decimal montantGroupe = 0m;
            decimal cumulGroupe = 0m;
            var moisGroupe = new decimal[12];
            var lignesFeuilles = new List<PrevisionGrilleLigneDto>();

            foreach (var rb in groupe.Rubriques)
            {
                byRb.TryGetValue(rb.IdRB, out var prev);
                var reps = CompactRepartitions(prev?.Repartitions);
                var montant = prev?.MontantAnnuel ?? 0m;
                var cumul = reps.Sum(r => r.Montant);
                montantGroupe += montant;
                cumulGroupe += cumul;
                foreach (var r in reps)
                {
                    if (r.Mois is >= 1 and <= 12)
                    {
                        moisGroupe[r.Mois - 1] += r.Montant;
                    }
                }

                lignesFeuilles.Add(new PrevisionGrilleLigneDto(
                    prev?.IdPrevision,
                    rb.IdRB,
                    rb.CodeRB,
                    rb.Libelle,
                    rb.ParentId,
                    rb.Niveau,
                    EstSection: false,
                    DetailBI: null,
                    MontantAnnuel: montant,
                    CumulMensuel: cumul,
                    Repartitions: reps,
                    IdGroupeRB: groupe.IdGroupeRB,
                    CodeGroupe: groupe.CodeGroupe,
                    LibelleGroupe: groupe.LibelleGroupe,
                    OrdreAffichageGroupe: groupe.Ordre));
            }

            var repsGroupe = CompactRepartitions(
                Enumerable.Range(1, 12)
                    .Where(m => moisGroupe[m - 1] != 0)
                    .Select(m => new RepartitionMensuelleDto((byte)m, moisGroupe[m - 1]))
                    .ToList());

            // Ligne de rupture Groupe N1 (non saisissable) — IdRB null pour ne pas confondre avec une RB.
            result.Add(new PrevisionGrilleLigneDto(
                null,
                IdRB: null,
                CodeRB: groupe.CodeGroupe,
                LibelleRB: groupe.LibelleGroupe,
                ParentIdRB: null,
                NiveauRB: 0,
                EstSection: true,
                DetailBI: null,
                MontantAnnuel: montantGroupe,
                CumulMensuel: cumulGroupe,
                Repartitions: repsGroupe,
                IdGroupeRB: groupe.IdGroupeRB,
                CodeGroupe: groupe.CodeGroupe,
                LibelleGroupe: groupe.LibelleGroupe,
                OrdreAffichageGroupe: groupe.Ordre));

            result.AddRange(lignesFeuilles);
        }

        return result;
    }

    private async Task ApresSaisieUbAsync(
        long idVersion,
        long idUB,
        long idUtilisateur,
        CancellationToken cancellationToken)
    {
        await _workflowUbService.EnsureExistsAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        await _workflowUbService.ReouvrirSiRejeteeApresSaisieAsync(
            idVersion, idUB, idUtilisateur, cancellationToken);
        // Ne PAS réouvrir VERSION_BUDGETAIRE ici : l'agrégat ne doit pas contaminer les autres UB.
    }

    private async Task GarantirModificationAutoriseeUbAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var statut = await _workflowUbService.GetStatutOperationnelAsync(idVersion, idUB, cancellationToken);
        if (!EstModificationAutorisee(statut))
        {
            throw new InvalidOperationException(
                $"L'unité budgétaire est au statut {StatutVersionBudgetaire.Normaliser(statut)}. " +
                "La modification des prévisions n'est autorisée qu'en BROUILLON ou REJETEE pour cette UB.");
        }
    }

    private static IReadOnlyList<RepartitionMensuelleDto> CompactRepartitions(
        IReadOnlyList<RepartitionMensuelleDto>? repartitions)
        => (repartitions ?? [])
            .Where(r => r.Montant != 0)
            .OrderBy(r => r.Mois)
            .ToList();

    private static IReadOnlyList<PrevisionGrilleLigneDto> ConstruireLignesBi(
        IReadOnlyList<PrevisionGrilleSourceDto> previsions)
        => previsions
            .OrderBy(p => p.DetailBI, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PrevisionGrilleLigneDto(
                p.IdPrevision,
                null,
                null,
                null,
                null,
                null,
                false,
                p.DetailBI,
                p.MontantAnnuel,
                p.Repartitions.Sum(r => r.Montant),
                p.Repartitions))
            .ToList();

    private static (decimal Montant, IReadOnlyList<RepartitionMensuelleDto> Repartitions) NormaliserMontants(
        string codeMode,
        decimal? montantAnnuel,
        IReadOnlyList<RepartitionMensuelleDto>? repartitions)
    {
        var reps = NormaliserRepartitions(repartitions);
        var annuel = montantAnnuel ?? 0m;
        if (annuel < 0)
        {
            throw new InvalidOperationException("Le montant annuel ne peut pas être négatif.");
        }

        if (codeMode.Equals(ModePrevisionCode.Mensuel, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var r in reps)
            {
                if (r.Montant < 0)
                {
                    throw new InvalidOperationException("Les montants mensuels ne peuvent pas être négatifs.");
                }
            }

            // Répartition définie → MontantAnnuel = SUM(mois). Sinon conserver le montant annuel seul.
            if (reps.Count > 0)
            {
                return (reps.Sum(r => r.Montant), reps);
            }

            return (annuel, []);
        }

        // Mode ANNUEL : montant global uniquement — aucune répartition créée ici.
        return (annuel, []);
    }

    private static IReadOnlyList<RepartitionMensuelleDto> NormaliserRepartitions(
        IReadOnlyList<RepartitionMensuelleDto>? repartitions)
    {
        if (repartitions is null || repartitions.Count == 0)
        {
            return [];
        }

        var map = new Dictionary<byte, decimal>();
        foreach (var r in repartitions)
        {
            if (r.Mois is < 1 or > 12)
            {
                throw new InvalidOperationException("Le mois de répartition doit être compris entre 1 et 12.");
            }

            if (r.Montant < 0)
            {
                throw new InvalidOperationException("Les montants mensuels ne peuvent pas être négatifs.");
            }

            if (map.ContainsKey(r.Mois))
            {
                throw new InvalidOperationException($"Le mois {r.Mois} est dupliqué dans la ventilation.");
            }

            if (r.Montant != 0)
            {
                map[r.Mois] = r.Montant;
            }
        }

        return map.OrderBy(x => x.Key).Select(x => new RepartitionMensuelleDto(x.Key, x.Value)).ToList();
    }

    private static bool LigneADuContenu(
        string codeMode,
        decimal montant,
        IReadOnlyList<RepartitionMensuelleDto> reps,
        string? detailBI)
    {
        if (!string.IsNullOrWhiteSpace(detailBI))
        {
            return true;
        }

        if (montant != 0)
        {
            return true;
        }

        return reps.Any(r => r.Montant != 0);
    }

    private static void VerifierAppartenanceContexte(
        PrevisionBudgetaireDto existing,
        SauvegarderGrillePrevisionRequest request,
        string codeType)
    {
        // Le mode (ANNUEL/MENSUEL) n'est pas discriminant : une RB = une prévision.
        if (existing.IdVersion != request.IdVersion
            || existing.IdTypeBudget != request.IdTypeBudget
            || existing.IdUB != request.IdUB)
        {
            throw new InvalidOperationException("La prévision n'appartient pas au contexte de grille sélectionné.");
        }

        if (codeType == TypeBudgetCode.ActionsExploitation
            && !string.Equals(
                NormaliserTexteOptionnel(existing.LibelleItemAE),
                NormaliserTexteOptionnel(request.LibelleItemAE),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La prévision n'appartient pas à l'action d'exploitation sélectionnée.");
        }

        if (codeType == TypeBudgetCode.BudgetInvestissement && existing.IdItemBI != request.IdItemBI)
        {
            throw new InvalidOperationException("La prévision n'appartient pas à l'item BI sélectionné.");
        }
    }

    public static bool EstModificationAutorisee(string statut)
    {
        var s = StatutVersionBudgetaire.Normaliser(statut);
        return s is StatutVersionBudgetaire.Brouillon or StatutVersionBudgetaire.Rejetee;
    }

    private static void RefuseSiRenseigne(long? value, string champ, string codeType)
    {
        if (value is > 0)
        {
            throw new InvalidOperationException($"Le champ {champ} est interdit pour une prévision {codeType}.");
        }
    }

    private static void RefuseSiTexte(string? value, string champ, string codeType)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Le champ {champ} est interdit pour une prévision {codeType}.");
        }
    }

    private static string NormaliserTexteObligatoire(string? value, string label)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"{label} est obligatoire.");
        }

        if (text.Length > 500)
        {
            throw new InvalidOperationException($"{label} ne peut pas dépasser 500 caractères.");
        }

        return text;
    }

    private static string? NormaliserTexteOptionnel(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private sealed record ContexteValide(
        string CodeType,
        string CodeMode,
        long? IdRB,
        long? IdItemBI,
        long? IdGroupeItemAE,
        string? LibelleItemAE,
        string? DetailBI);
}

public class GroupeItemAEService : IGroupeItemAEService
{
    private readonly IGroupeItemAERepository _repository;

    public GroupeItemAEService(IGroupeItemAERepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<GroupeItemAEDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<GroupeItemAEDto?> GetByIdAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idGroupeItemAE, cancellationToken);

    public async Task<GroupeItemAEDto> CreateAsync(CreateGroupeItemAERequest request, CancellationToken cancellationToken = default)
    {
        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé du groupe AE est obligatoire.");
        }

        if (libelle.Length > 200)
        {
            throw new InvalidOperationException("Le libellé du groupe AE ne peut pas dépasser 200 caractères.");
        }

        return await _repository.CreateAsync(libelle, request.Actif ?? true, cancellationToken);
    }

    public async Task<GroupeItemAEDto?> UpdateAsync(
        long idGroupeItemAE,
        UpdateGroupeItemAERequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idGroupeItemAE, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
        {
            throw new InvalidOperationException("Le libellé du groupe AE est obligatoire.");
        }

        return await _repository.UpdateAsync(idGroupeItemAE, libelle, request.Actif ?? existing.Actif, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idGroupeItemAE, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idGroupeItemAE, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var count = await _repository.CountPrevisionsAsync(idGroupeItemAE, cancellationToken);
        if (count > 0)
        {
            throw new InvalidOperationException(
                "Ce groupe AE ne peut pas être supprimé car il est utilisé par une ou plusieurs prévisions.");
        }

        return await _repository.DeleteAsync(idGroupeItemAE, cancellationToken);
    }
}
