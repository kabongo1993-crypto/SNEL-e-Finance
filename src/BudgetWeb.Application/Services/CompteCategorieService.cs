using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class CompteCategorieService : ICompteCategorieService
{
    private readonly ICompteCategorieRepository _repository;
    private readonly ICompteFinancierRepository _comptes;
    private readonly ICategorieCompteRepository _categories;
    private readonly ICurrentUserService _currentUser;

    public CompteCategorieService(
        ICompteCategorieRepository repository,
        ICompteFinancierRepository comptes,
        ICategorieCompteRepository categories,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _comptes = comptes;
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CompteCategorieDto>> ListByCompteAsync(
        long idCompte,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        if (!await _comptes.ExistsIdAsync(idCompte, cancellationToken))
            throw new KeyNotFoundException("Compte introuvable.");

        var rows = await _repository.ListByCompteAsync(idCompte, cancellationToken);
        return rows
            .OrderByDescending(r => r.DateFin == null)
            .ThenByDescending(r => r.DateDebut)
            .ThenBy(r => r.IdCompteCategorie)
            .Select(Map)
            .ToList();
    }

    public async Task<CompteCategorieDto> CreateAsync(
        long idCompte,
        UpsertCompteCategorieRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        CompteCategorie? created = null;
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var compte = await _comptes.GetByIdAsync(idCompte, cancellationToken)
                ?? throw new KeyNotFoundException("Compte introuvable.");
            var categorie = await ResoudreCategorieNouvelleAsync(request.CategorieId, cancellationToken);
            CompteCategorieRules.ValiderPeriode(request.DateDebut, request.DateFin);
            if (request.DateFin is null && !compte.Actif)
                throw new InvalidOperationException(
                    "Impossible de créer une affectation active sur un compte inactif.");

            var existantes = await _repository.ListByCompteAsync(idCompte, cancellationToken);
            RefuserChevauchement(existantes, request.DateDebut, request.DateFin, excludeId: null);

            created = await _repository.AddAsync(
                new CompteCategorie
                {
                    FK_Compte = idCompte,
                    FK_CategorieCompte = categorie.IdCategorieCompte,
                    DateDebut = request.DateDebut,
                    DateFin = request.DateFin,
                },
                cancellationToken);
        }, cancellationToken);

        var reloaded = await _repository.GetByIdAsync(created!.IdCompteCategorie, cancellationToken)
            ?? created;
        return Map(reloaded);
    }

    public async Task<CompteCategorieDto?> UpdateAsync(
        long idCompte,
        long idCompteCategorie,
        UpsertCompteCategorieRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        CompteCategorie? entity = null;
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            var compte = await _comptes.GetByIdAsync(idCompte, cancellationToken)
                ?? throw new KeyNotFoundException("Compte introuvable.");
            entity = await _repository.GetTrackedByIdAsync(idCompteCategorie, cancellationToken);
            if (entity is null || entity.FK_Compte != idCompte)
                return;

            var categorie = await ResoudreCategoriePourMajAsync(
                request.CategorieId,
                entity.FK_CategorieCompte,
                cancellationToken);
            CompteCategorieRules.ValiderPeriode(request.DateDebut, request.DateFin);
            if (request.DateFin is null && !compte.Actif)
                throw new InvalidOperationException(
                    "Impossible d'activer une affectation sur un compte inactif.");

            var existantes = await _repository.ListByCompteAsync(idCompte, cancellationToken);
            RefuserChevauchement(existantes, request.DateDebut, request.DateFin, idCompteCategorie);

            entity.FK_CategorieCompte = categorie.IdCategorieCompte;
            entity.DateDebut = request.DateDebut;
            entity.DateFin = request.DateFin;
            await _repository.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        if (entity is null)
            return null;
        var reloaded = await _repository.GetByIdAsync(idCompteCategorie, cancellationToken) ?? entity;
        return Map(reloaded);
    }

    public async Task<CompteCategorieDto?> CloturerAsync(
        long idCompte,
        long idCompteCategorie,
        CloturerCompteCategorieRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        CompteCategorie? entity = null;
        await _repository.ExecuteInTransactionAsync(async () =>
        {
            if (!await _comptes.ExistsIdAsync(idCompte, cancellationToken))
                throw new KeyNotFoundException("Compte introuvable.");

            entity = await _repository.GetTrackedByIdAsync(idCompteCategorie, cancellationToken);
            if (entity is null || entity.FK_Compte != idCompte)
                return;
            if (entity.DateFin is not null)
                throw new InvalidOperationException("Cette affectation est déjà clôturée.");

            CompteCategorieRules.ValiderPeriode(entity.DateDebut, request.DateFin);
            var existantes = await _repository.ListByCompteAsync(idCompte, cancellationToken);
            RefuserChevauchement(existantes, entity.DateDebut, request.DateFin, idCompteCategorie);

            entity.DateFin = request.DateFin;
            await _repository.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        if (entity is null)
            return null;
        var reloaded = await _repository.GetByIdAsync(idCompteCategorie, cancellationToken) ?? entity;
        return Map(reloaded);
    }

    public async Task<ImportCompteCategoriesPreviewDto> PreviewImportAsync(
        ImportCompteCategoriesPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var ctx = await LoadImportContextAsync(cancellationToken);
        var raws = request.Lignes ?? [];
        var lignes = Analyser(raws, ctx);
        return new ImportCompteCategoriesPreviewDto(
            string.IsNullOrWhiteSpace(request.NomFichier) ? "import.xlsx" : request.NomFichier.Trim(),
            lignes.Count,
            lignes,
            Resume(lignes, raws));
    }

    public async Task<ImportCompteCategoriesResultDto> ImportAsync(
        ImportCompteCategoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var ctx = await LoadImportContextAsync(cancellationToken);
        var raws = request.Lignes ?? [];
        var lignes = Analyser(raws, ctx);
        var aImporter = lignes.Where(l => l.Statut == "a_importer").ToList();

        var entities = new List<CompteCategorie>();
        foreach (var ligne in aImporter)
        {
            var raw = raws.First(r => r.LigneExcel == ligne.LigneExcel);
            var built = BuildFromRaw(raw);
            if (built is null)
                continue;
            entities.Add(built);
            ctx.ExistingIds.Add(built.IdCompteCategorie);
            ctx.Existantes.Add(built);
        }

        if (entities.Count > 0)
            await _repository.AddRangeWithExplicitIdsAsync(entities, cancellationToken);

        var resume = Resume(lignes, raws);
        return new ImportCompteCategoriesResultDto(
            lignes.Count,
            entities.Count,
            lignes.Count(l => l.Statut is "doublon_fichier" or "deja_existant"),
            lignes.Count(l => l.Statut is "erreur" or "conflit_periode"),
            lignes,
            resume);
    }

    private async Task<CategorieCompte> ResoudreCategorieNouvelleAsync(
        long idCategorie,
        CancellationToken cancellationToken)
    {
        var categorie = await _categories.GetByIdAsync(idCategorie, cancellationToken)
            ?? throw new InvalidOperationException("Catégorie introuvable.");
        if (!categorie.Actif)
            throw new InvalidOperationException("Une catégorie inactive ne peut pas être affectée.");
        return categorie;
    }

    private async Task<CategorieCompte> ResoudreCategoriePourMajAsync(
        long idCategorie,
        long idCategorieActuelle,
        CancellationToken cancellationToken)
    {
        var categorie = await _categories.GetByIdAsync(idCategorie, cancellationToken)
            ?? throw new InvalidOperationException("Catégorie introuvable.");
        if (!categorie.Actif && categorie.IdCategorieCompte != idCategorieActuelle)
            throw new InvalidOperationException("Une catégorie inactive ne peut pas être affectée.");
        return categorie;
    }

    private static void RefuserChevauchement(
        IReadOnlyList<CompteCategorie> existantes,
        DateOnly dateDebut,
        DateOnly? dateFin,
        long? excludeId)
    {
        foreach (var autre in existantes.Where(e => excludeId is null || e.IdCompteCategorie != excludeId))
        {
            if (!CompteCategorieRules.PeriodesSeChevauchent(dateDebut, dateFin, autre.DateDebut, autre.DateFin))
                continue;
            throw new InvalidOperationException(
                CompteCategorieRules.MessageConflit(autre.DateFin is null, dateFin is null));
        }
    }

    private async Task<ImportContext> LoadImportContextAsync(CancellationToken cancellationToken)
    {
        var comptes = await _comptes.ListAsync(null, cancellationToken);
        var categories = await _categories.ListAsync(null, cancellationToken);
        var existantes = await _repository.ListAllAsync(cancellationToken);
        var existingIds = existantes.Select(e => e.IdCompteCategorie).ToHashSet();
        return new ImportContext(comptes, categories, existantes.ToList(), existingIds);
    }

    private static List<ImportCompteCategorieLigneDto> Analyser(
        IReadOnlyList<ImportCompteCategorieRawRequest> raws,
        ImportContext ctx)
    {
        var firstId = new Dictionary<long, int>();
        var firstCombo = new Dictionary<string, int>(StringComparer.Ordinal);
        var candidates = new List<(ImportCompteCategorieRawRequest Raw, CompteCategorie Built, bool Sentinelle)>();
        var lignes = new List<ImportCompteCategorieLigneDto>(raws.Count);
        var ordered = raws.OrderBy(r => r.LigneExcel).ToList();

        foreach (var raw in ordered)
        {
            var parsed = AnalyserLigne(raw, ctx, firstId, firstCombo);
            if (parsed.Ligne.Statut == "a_importer" && parsed.Built is not null)
            {
                candidates.Add((raw, parsed.Built, parsed.Sentinelle));
                lignes.Add(parsed.Ligne);
            }
            else
            {
                lignes.Add(parsed.Ligne);
            }
        }

        MarquerChevauchementsFichier(lignes, candidates, ctx);
        return lignes.OrderBy(l => l.LigneExcel).ToList();
    }

    private static void MarquerChevauchementsFichier(
        List<ImportCompteCategorieLigneDto> lignes,
        List<(ImportCompteCategorieRawRequest Raw, CompteCategorie Built, bool Sentinelle)> candidates,
        ImportContext ctx)
    {
        var byLigne = lignes.ToDictionary(l => l.LigneExcel);
        var accepted = new List<CompteCategorie>();

        foreach (var (raw, built, _) in candidates.OrderBy(c => c.Raw.LigneExcel))
        {
            var overlapDb = ctx.Existantes.FirstOrDefault(e =>
                e.FK_Compte == built.FK_Compte
                && CompteCategorieRules.PeriodesSeChevauchent(built.DateDebut, built.DateFin, e.DateDebut, e.DateFin));
            if (overlapDb is not null)
            {
                byLigne[raw.LigneExcel] = WithStatut(
                    byLigne[raw.LigneExcel],
                    "conflit_periode",
                    CompteCategorieRules.MessageConflit(overlapDb.DateFin is null, built.DateFin is null),
                    "période",
                    PeriodeLibelle(built));
                continue;
            }

            var overlapFile = accepted.FirstOrDefault(e =>
                e.FK_Compte == built.FK_Compte
                && CompteCategorieRules.PeriodesSeChevauchent(built.DateDebut, built.DateFin, e.DateDebut, e.DateFin));
            if (overlapFile is not null)
            {
                byLigne[raw.LigneExcel] = WithStatut(
                    byLigne[raw.LigneExcel],
                    "conflit_periode",
                    CompteCategorieRules.MessageConflit(overlapFile.DateFin is null, built.DateFin is null),
                    "période",
                    PeriodeLibelle(built));
                continue;
            }

            accepted.Add(built);
        }

        for (var i = 0; i < lignes.Count; i++)
            lignes[i] = byLigne[lignes[i].LigneExcel];
    }

    private static (ImportCompteCategorieLigneDto Ligne, CompteCategorie? Built, bool Sentinelle) AnalyserLigne(
        ImportCompteCategorieRawRequest raw,
        ImportContext ctx,
        Dictionary<long, int> firstId,
        Dictionary<string, int> firstCombo)
    {
        var idRelation = CompteCategorieRules.ParseId(raw.IdCompteCategorie);
        var idCompte = CompteCategorieRules.ParseId(raw.IdCompte);
        var idCategorie = CompteCategorieRules.ParseId(raw.IdCategorieCompte);
        var dateFin = CompteCategorieRules.ParseDateFinImport(raw.DateFin, out var sentinelle);
        var dateDebut = CompteCategorieRules.ParseDate(raw.DateDebut);
        var compteLibelle = CompteLibelle(ctx, idCompte);
        var categorieLibelle = CategorieLibelle(ctx, idCategorie);

        ImportCompteCategorieLigneDto Ok(string statut, string resultat, string? champ = null, string? valeur = null)
            => new(
                raw.LigneExcel,
                idRelation,
                idCompte,
                compteLibelle,
                idCategorie,
                categorieLibelle,
                CompteCategorieRules.FormaterDate(dateDebut),
                dateFin is null && (sentinelle || string.IsNullOrWhiteSpace(raw.DateFin))
                    ? ""
                    : CompteCategorieRules.FormaterDate(dateFin),
                statut,
                resultat,
                champ,
                valeur);

        if (idRelation is null)
            return (Ok("erreur", "IDT_COMPTE_AVEC_CATEGORIE historique obligatoire", "IDT_COMPTE_AVEC_CATEGORIE", raw.IdCompteCategorie), null, sentinelle);

        if (firstId.TryGetValue(idRelation.Value, out var firstLigneId))
            return (Ok("erreur", $"IDT_COMPTE_AVEC_CATEGORIE déjà présent dans le fichier (ligne {firstLigneId})", "IDT_COMPTE_AVEC_CATEGORIE", raw.IdCompteCategorie), null, sentinelle);
        firstId[idRelation.Value] = raw.LigneExcel;

        if (ctx.ExistingIds.Contains(idRelation.Value))
            return (Ok("deja_existant", "IdCompteCategorie déjà existant", "IDT_COMPTE_AVEC_CATEGORIE", raw.IdCompteCategorie), null, sentinelle);

        if (idCompte is null)
            return (Ok("erreur", "Id_Compte obligatoire", "Id_Compte", raw.IdCompte), null, sentinelle);
        if (!ctx.Comptes.ContainsKey(idCompte.Value))
            return (Ok("erreur", $"Compte {idCompte} introuvable", "Id_Compte", raw.IdCompte), null, sentinelle);

        if (idCategorie is null)
            return (Ok("erreur", "IDT_CATEGORIE_COMPTE obligatoire", "IDT_CATEGORIE_COMPTE", raw.IdCategorieCompte), null, sentinelle);
        if (!ctx.Categories.ContainsKey(idCategorie.Value))
            return (Ok("erreur", $"Catégorie {idCategorie} introuvable", "IDT_CATEGORIE_COMPTE", raw.IdCategorieCompte), null, sentinelle);

        if (dateDebut is null)
            return (Ok("erreur", "Date de début obligatoire ou invalide", "Date_Debut", raw.DateDebut), null, sentinelle);

        if (!string.IsNullOrWhiteSpace(raw.DateFin)
            && !sentinelle
            && dateFin is null)
            return (Ok("erreur", "Date de fin invalide", "Date_Fin", raw.DateFin), null, sentinelle);

        if (dateFin is DateOnly fin && fin < dateDebut.Value)
            return (Ok("erreur", "Date de fin antérieure à la date de début", "Date_Fin", raw.DateFin), null, sentinelle);

        var combo = ComboKey(idCompte.Value, idCategorie.Value, dateDebut.Value, dateFin);
        if (firstCombo.TryGetValue(combo, out var firstComboLigne))
            return (Ok("doublon_fichier", $"Affectation déjà présente dans le fichier (ligne {firstComboLigne})", "période", PeriodeLibelle(dateDebut.Value, dateFin)), null, sentinelle);
        firstCombo[combo] = raw.LigneExcel;

        if (ctx.Existantes.Any(e =>
                e.FK_Compte == idCompte.Value
                && e.FK_CategorieCompte == idCategorie.Value
                && e.DateDebut == dateDebut.Value
                && e.DateFin == dateFin))
            return (Ok("deja_existant", "Affectation déjà existante", "période", PeriodeLibelle(dateDebut.Value, dateFin)), null, sentinelle);

        var built = new CompteCategorie
        {
            IdCompteCategorie = idRelation.Value,
            FK_Compte = idCompte.Value,
            FK_CategorieCompte = idCategorie.Value,
            DateDebut = dateDebut.Value,
            DateFin = dateFin,
        };
        var resultat = sentinelle
            ? "À importer (Date_Fin 1900-01-01 convertie en NULL)"
            : "À importer";
        return (Ok("a_importer", resultat), built, sentinelle);
    }

    private static CompteCategorie? BuildFromRaw(ImportCompteCategorieRawRequest raw)
    {
        var idRelation = CompteCategorieRules.ParseId(raw.IdCompteCategorie);
        var idCompte = CompteCategorieRules.ParseId(raw.IdCompte);
        var idCategorie = CompteCategorieRules.ParseId(raw.IdCategorieCompte);
        var dateDebut = CompteCategorieRules.ParseDate(raw.DateDebut);
        var dateFin = CompteCategorieRules.ParseDateFinImport(raw.DateFin, out _);
        if (idRelation is null || idCompte is null || idCategorie is null || dateDebut is null)
            return null;
        return new CompteCategorie
        {
            IdCompteCategorie = idRelation.Value,
            FK_Compte = idCompte.Value,
            FK_CategorieCompte = idCategorie.Value,
            DateDebut = dateDebut.Value,
            DateFin = dateFin,
        };
    }

    private static ImportCompteCategorieLigneDto WithStatut(
        ImportCompteCategorieLigneDto ligne,
        string statut,
        string resultat,
        string? champ,
        string? valeur)
        => ligne with { Statut = statut, Resultat = resultat, Champ = champ, ValeurRecue = valeur };

    private static ImportCompteCategoriesResumeDto Resume(
        IReadOnlyList<ImportCompteCategorieLigneDto> lignes,
        IReadOnlyList<ImportCompteCategorieRawRequest> raws)
        => new(
            lignes.Count,
            lignes.Count(l => l.Statut == "a_importer"),
            lignes.Count(l => l.Statut == "doublon_fichier"),
            lignes.Count(l => l.Statut == "deja_existant"),
            lignes.Count(l => l.Statut == "erreur"),
            lignes.Count(l => l.Statut == "conflit_periode"),
            raws.Count(r => CompteCategorieRules.EstDateFinSentinelle1900(r.DateFin)));

    private static string ComboKey(long idCompte, long idCategorie, DateOnly debut, DateOnly? fin)
        => $"{idCompte}|{idCategorie}|{debut:yyyy-MM-dd}|{CompteCategorieRules.FormaterDate(fin)}";

    private static string PeriodeLibelle(CompteCategorie e)
        => PeriodeLibelle(e.DateDebut, e.DateFin);

    private static string PeriodeLibelle(DateOnly debut, DateOnly? fin)
        => $"{CompteCategorieRules.FormaterDate(debut)} → {(fin is null ? "—" : CompteCategorieRules.FormaterDate(fin))}";

    private static string CompteLibelle(ImportContext ctx, long? id)
        => id is long i && ctx.Comptes.TryGetValue(i, out var c)
            ? $"{c.NumeroCompte} — {c.LibelleCompte}"
            : id?.ToString() ?? "";

    private static string CategorieLibelle(ImportContext ctx, long? id)
        => id is long i && ctx.Categories.TryGetValue(i, out var c)
            ? c.Libelle
            : id?.ToString() ?? "";

    private static CompteCategorieDto Map(CompteCategorie e)
        => new(
            e.IdCompteCategorie,
            e.FK_Compte,
            e.Compte?.NumeroCompte ?? string.Empty,
            e.Compte?.LibelleCompte ?? string.Empty,
            e.FK_CategorieCompte,
            e.CategorieCompte?.Libelle ?? string.Empty,
            e.CategorieCompte?.Actif ?? true,
            e.DateDebut,
            e.DateFin,
            e.DateFin is null);

    private void ExigerLecture()
    {
        if (_currentUser.HasPermission(AppPermissions.PaiementsLire)
            || _currentUser.HasPermission(AppPermissions.PaiementsEcrire)
            || _currentUser.HasPermission(AppPermissions.PaiementsSoumettre)
            || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
            || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
            || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget)
            || _currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : paiements.lire.");
    }

    private void ExigerEcriture()
    {
        if (_currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : referentiels.ecrire.");
    }

    private sealed class ImportContext
    {
        public ImportContext(
            IReadOnlyList<CompteFinancier> comptes,
            IReadOnlyList<CategorieCompte> categories,
            List<CompteCategorie> existantes,
            HashSet<long> existingIds)
        {
            Comptes = comptes.ToDictionary(c => c.IdCompte);
            Categories = categories.ToDictionary(c => c.IdCategorieCompte);
            Existantes = existantes;
            ExistingIds = existingIds;
        }

        public Dictionary<long, CompteFinancier> Comptes { get; }
        public Dictionary<long, CategorieCompte> Categories { get; }
        public List<CompteCategorie> Existantes { get; }
        public HashSet<long> ExistingIds { get; }
    }
}
