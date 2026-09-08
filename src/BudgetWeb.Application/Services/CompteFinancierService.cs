using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class CompteFinancierService : ICompteFinancierService
{
    private const int MaxNumero = 50;
    private const int MaxLibelle = 200;

    private readonly ICompteFinancierRepository _repository;
    private readonly IBanqueRepository _banques;
    private readonly IDirectionRepository _directions;
    private readonly ITypeCompteRepository _types;
    private readonly IDeviseRepository _devises;
    private readonly IProvinceRepository _provinces;
    private readonly IAuthRepository _utilisateurs;
    private readonly ICurrentUserService _currentUser;

    public CompteFinancierService(
        ICompteFinancierRepository repository,
        IBanqueRepository banques,
        IDirectionRepository directions,
        ITypeCompteRepository types,
        IDeviseRepository devises,
        IProvinceRepository provinces,
        IAuthRepository utilisateurs,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _banques = banques;
        _directions = directions;
        _types = types;
        _devises = devises;
        _provinces = provinces;
        _utilisateurs = utilisateurs;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CompteFinancierDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<CompteFinancierDto?> GetByIdAsync(long idCompte, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idCompte, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<CompteFinancierDto> CreateAsync(
        CreateCompteFinancierRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var refs = await ResoudreRefsAsync(
            request.IdBanque,
            request.IdDirection,
            request.CodeTypeCompte,
            request.IdDevise,
            request.IdProvince,
            request.IdUtilisateur,
            exigerActifs: true,
            cancellationToken);
        var numero = ValiderNumero(request.NumeroCompte);
        var libelle = ValiderLibelle(request.LibelleCompte);
        if (await _repository.ExistsNumeroAsync(refs.Banque.IdBanque, numero, null, cancellationToken))
            throw new InvalidOperationException("Un compte portant ce numéro existe déjà pour cette banque.");

        var (actif, cloture) = NormaliserActif(request.Actif, null);
        var created = await _repository.AddAsync(
            new CompteFinancier
            {
                NumeroCompte = numero,
                LibelleCompte = libelle,
                FK_Banque = refs.Banque.IdBanque,
                FK_Direction = refs.Direction.IdDirection,
                FK_TypeCompte = refs.Type.Code,
                FK_Devise = refs.Devise.IdDevise,
                FK_Province = refs.Province?.IdProvince,
                FK_Utilisateur = refs.Utilisateur?.IdUtilisateur,
                DateCreation = DateTime.UtcNow,
                DateCloture = cloture,
                Actif = actif,
            },
            cancellationToken);

        var reloaded = await _repository.GetByIdAsync(created.IdCompte, cancellationToken)
            ?? created;
        return Map(reloaded);
    }

    public async Task<CompteFinancierDto?> UpdateAsync(
        long idCompte,
        UpdateCompteFinancierRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetTrackedByIdAsync(idCompte, cancellationToken);
        if (entity is null)
            return null;

        var refs = await ResoudreRefsAsync(
            request.IdBanque,
            request.IdDirection,
            request.CodeTypeCompte,
            request.IdDevise,
            request.IdProvince,
            request.IdUtilisateur,
            exigerActifs: false,
            cancellationToken);
        var numero = ValiderNumero(request.NumeroCompte);
        var libelle = ValiderLibelle(request.LibelleCompte);
        if (await _repository.ExistsNumeroAsync(refs.Banque.IdBanque, numero, idCompte, cancellationToken))
            throw new InvalidOperationException("Un compte portant ce numéro existe déjà pour cette banque.");

        var (actif, cloture) = NormaliserActif(request.Actif, entity.DateCloture);
        entity.NumeroCompte = numero;
        entity.LibelleCompte = libelle;
        entity.FK_Banque = refs.Banque.IdBanque;
        entity.FK_Direction = refs.Direction.IdDirection;
        entity.FK_TypeCompte = refs.Type.Code;
        entity.FK_Devise = refs.Devise.IdDevise;
        entity.FK_Province = refs.Province?.IdProvince;
        entity.FK_Utilisateur = refs.Utilisateur?.IdUtilisateur;
        entity.Actif = actif;
        entity.DateCloture = cloture;
        entity.DateModification = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);

        var reloaded = await _repository.GetByIdAsync(idCompte, cancellationToken) ?? entity;
        return Map(reloaded);
    }

    public async Task<ImportComptesPreviewDto> PreviewImportAsync(
        ImportComptesPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var ctx = await LoadImportContextAsync(cancellationToken);
        var lignes = Analyser(request.Lignes ?? [], ctx);
        return new ImportComptesPreviewDto(
            string.IsNullOrWhiteSpace(request.NomFichier) ? "import.xlsx" : request.NomFichier.Trim(),
            lignes.Count,
            lignes,
            Resume(lignes));
    }

    public async Task<ImportComptesResultDto> ImportAsync(
        ImportComptesRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var ctx = await LoadImportContextAsync(cancellationToken);
        var lignes = Analyser(request.Lignes ?? [], ctx);
        var aImporter = lignes.Where(l => l.Statut == "a_importer").ToList();

        var entities = new List<CompteFinancier>();
        foreach (var ligne in aImporter)
        {
            var raw = (request.Lignes ?? []).First(r => r.LigneExcel == ligne.LigneExcel);
            var built = BuildFromRaw(raw, ctx);
            if (built is null)
                continue;
            entities.Add(built);
            ctx.ExistingIds.Add(built.IdCompte);
            ctx.ExistingNumeros.Add(Key(built.FK_Banque, built.NumeroCompte));
        }

        if (entities.Count > 0)
            await _repository.AddRangeWithExplicitIdsAsync(entities, cancellationToken);

        var ignores = lignes.Count(l => l.Statut is "doublon_fichier" or "deja_existant");
        var erreurs = lignes.Count(l => l.Statut == "erreur");
        return new ImportComptesResultDto(
            lignes.Count,
            entities.Count,
            ignores,
            erreurs,
            lignes);
    }

    private async Task<Refs> ResoudreRefsAsync(
        string fkBanque,
        long fkDirection,
        string fkTypeCompte,
        long fkDevise,
        string? fkProvince,
        long? fkUtilisateur,
        bool exigerActifs,
        CancellationToken cancellationToken)
    {
        var banqueId = (fkBanque ?? string.Empty).Trim();
        if (banqueId.Length == 0)
            throw new ArgumentException("La banque est obligatoire.");
        var banque = (await _banques.ListAsync(null, cancellationToken))
            .FirstOrDefault(b => string.Equals(b.IdBanque, banqueId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Banque {banqueId} introuvable.");
        if (exigerActifs && !banque.Actif)
            throw new InvalidOperationException($"Banque {banque.IdBanque} introuvable parmi les banques actives.");

        var direction = await _directions.GetByIdAsync(fkDirection, cancellationToken)
            ?? throw new InvalidOperationException("Direction introuvable.");
        if (exigerActifs && !direction.Actif)
            throw new InvalidOperationException("La direction indiquée est inactive.");

        var typeCode = (fkTypeCompte ?? string.Empty).Trim();
        var type = await _types.GetByCodeAsync(typeCode, cancellationToken)
            ?? throw new InvalidOperationException($"Type de compte {typeCode} introuvable.");
        if (exigerActifs && !type.Actif)
            throw new InvalidOperationException("Le type de compte indiqué est inactif.");

        var devise = await _devises.GetByIdAsync(fkDevise, cancellationToken)
            ?? throw new InvalidOperationException("Devise introuvable.");
        if (exigerActifs && !devise.Actif)
            throw new InvalidOperationException("La devise indiquée est inactive.");

        Province? province = null;
        var provId = string.IsNullOrWhiteSpace(fkProvince) ? null : fkProvince.Trim();
        if (provId is not null)
        {
            province = await _provinces.GetByIdAsync(provId, cancellationToken)
                ?? throw new InvalidOperationException($"Province {provId} introuvable.");
            if (exigerActifs && !province.Actif)
                throw new InvalidOperationException("La province indiquée est inactive.");
        }

        Utilisateur? utilisateur = null;
        if (fkUtilisateur is > 0)
        {
            utilisateur = await _utilisateurs.FindByIdAsync(fkUtilisateur.Value, cancellationToken)
                ?? throw new InvalidOperationException("Utilisateur introuvable.");
        }

        return new Refs(banque, direction, type, devise, province, utilisateur);
    }

    private async Task<ImportContext> LoadImportContextAsync(CancellationToken cancellationToken)
    {
        var banques = await _banques.ListAsync(null, cancellationToken);
        var directions = await _directions.ListAsync(null, cancellationToken);
        var types = await _types.ListAsync(null, cancellationToken);
        var devises = await _devises.ListAsync(null, cancellationToken);
        var provinces = await _provinces.ListAsync(null, cancellationToken);
        var existingIds = (await _repository.ListIdsAsync(cancellationToken)).ToHashSet();
        var existingComptes = await _repository.ListAsync(null, cancellationToken);
        var existingNumeros = existingComptes
            .Select(c => Key(c.FK_Banque, c.NumeroCompte))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new ImportContext(banques, directions, types, devises, provinces, existingIds, existingNumeros);
    }

    private static List<ImportCompteLigneDto> Analyser(
        IReadOnlyList<ImportCompteRawRequest> raws,
        ImportContext ctx)
    {
        var firstNumero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstId = new Dictionary<long, int>();
        var lignes = new List<ImportCompteLigneDto>(raws.Count);

        foreach (var raw in raws.OrderBy(r => r.LigneExcel))
        {
            var ligne = AnalyserLigne(raw, ctx, firstNumero, firstId);
            lignes.Add(ligne);
        }

        return lignes;
    }

    private static ImportCompteLigneDto AnalyserLigne(
        ImportCompteRawRequest raw,
        ImportContext ctx,
        Dictionary<string, int> firstNumero,
        Dictionary<long, int> firstId)
    {
        var numero = (raw.NumeroCompte ?? string.Empty).Trim();
        var libelle = (raw.LibelleCompte ?? string.Empty).Trim();
        var banqueRaw = (raw.Banque ?? string.Empty).Trim();
        var typeRaw = (raw.TypeCompte ?? string.Empty).Trim();
        var deviseRaw = (raw.Devise ?? string.Empty).Trim();
        var directionRaw = (raw.Direction ?? string.Empty).Trim();
        var provinceRaw = string.IsNullOrWhiteSpace(raw.IdtProvince) ? null : raw.IdtProvince.Trim();
        var idCompte = CompteImportRules.ParseIdCompte(raw.IdCompte);

        ImportCompteLigneDto Ok(string statut, string resultat, string? champ = null, string? valeur = null)
            => new(
                raw.LigneExcel,
                idCompte,
                numero,
                libelle,
                banqueRaw,
                typeRaw,
                deviseRaw,
                directionRaw,
                provinceRaw,
                statut,
                resultat,
                champ,
                valeur);

        if (numero.Length == 0)
            return Ok("erreur", "Numéro de compte obligatoire", "Numero_Compte", raw.NumeroCompte);
        if (numero.Length > MaxNumero)
            return Ok("erreur", $"Numéro de compte trop long (max. {MaxNumero})", "Numero_Compte", numero);
        if (libelle.Length == 0)
            return Ok("erreur", "Libellé obligatoire", "Libelle_Compte", raw.LibelleCompte);
        if (libelle.Length > MaxLibelle)
            return Ok("erreur", $"Libellé trop long (max. {MaxLibelle})", "Libelle_Compte", libelle);

        var banque = CompteImportRules.ResolveBanque(banqueRaw, ctx.Banques);
        if (banque is null)
            return Ok("erreur", $"Banque {banqueRaw} introuvable", "Banque", banqueRaw);

        var type = CompteImportRules.ResolveTypeCompte(typeRaw, ctx.Types);
        if (type is null)
            return Ok("erreur", $"Type de compte {typeRaw} introuvable", "Type_Compte", typeRaw);

        var devise = CompteImportRules.ResolveDevise(deviseRaw, ctx.Devises);
        if (devise is null)
            return Ok("erreur", $"Devise {deviseRaw} introuvable", "Devise", deviseRaw);

        var direction = CompteImportRules.ResolveDirection(directionRaw, ctx.Directions);
        if (direction is null)
            return Ok("erreur", $"Direction {directionRaw} introuvable", "Direction", directionRaw);

        if (provinceRaw is not null)
        {
            var province = CompteImportRules.ResolveProvince(provinceRaw, ctx.Provinces);
            if (province is null)
                return Ok("erreur", $"Province {provinceRaw} introuvable", "IDT_PROVINCE", provinceRaw);
        }

        var dateCreation = CompteImportRules.ParseExcelDate(raw.DateCreation);
        if (dateCreation is null && !string.IsNullOrWhiteSpace(raw.DateCreation)
            && !CompteImportRules.EstDateSentinelle(raw.DateCreation))
            return Ok("erreur", "Date de création invalide", "Date_Creation", raw.DateCreation);

        if (idCompte is long id)
        {
            if (firstId.TryGetValue(id, out var firstLigneId))
                return Ok("erreur", $"Id_Compte déjà présent dans le fichier (ligne {firstLigneId})", "Id_Compte", raw.IdCompte);
            if (ctx.ExistingIds.Contains(id))
                return Ok("deja_existant", "Id_Compte déjà existant", "Id_Compte", raw.IdCompte);
            firstId[id] = raw.LigneExcel;
        }
        else if (!string.IsNullOrWhiteSpace(raw.IdCompte))
        {
            return Ok("erreur", "Id_Compte historique invalide", "Id_Compte", raw.IdCompte);
        }
        else
        {
            return Ok("erreur", "Id_Compte historique obligatoire pour l'import initial", "Id_Compte", raw.IdCompte);
        }

        var key = Key(banque.IdBanque, numero);
        if (firstNumero.TryGetValue(key, out var firstLigne))
            return Ok("doublon_fichier", $"Numéro de compte déjà existant dans le fichier (ligne {firstLigne})", "Numero_Compte", numero);
        if (ctx.ExistingNumeros.Contains(key))
            return Ok("deja_existant", "Numéro de compte déjà existant", "Numero_Compte", numero);

        firstNumero[key] = raw.LigneExcel;
        return Ok("a_importer", "À importer");
    }

    private static CompteFinancier? BuildFromRaw(ImportCompteRawRequest raw, ImportContext ctx)
    {
        var idCompte = CompteImportRules.ParseIdCompte(raw.IdCompte);
        var banque = CompteImportRules.ResolveBanque(raw.Banque, ctx.Banques);
        var type = CompteImportRules.ResolveTypeCompte(raw.TypeCompte, ctx.Types);
        var devise = CompteImportRules.ResolveDevise(raw.Devise, ctx.Devises);
        var direction = CompteImportRules.ResolveDirection(raw.Direction, ctx.Directions);
        if (idCompte is null || banque is null || type is null || devise is null || direction is null)
            return null;

        var province = CompteImportRules.ResolveProvince(raw.IdtProvince, ctx.Provinces);
        var (actif, cloture) = CompteImportRules.InterpretEtat(raw.Etat, raw.DateCloture);
        var dateCreation = CompteImportRules.ParseExcelDate(raw.DateCreation) ?? DateTime.UtcNow;

        return new CompteFinancier
        {
            IdCompte = idCompte.Value,
            NumeroCompte = (raw.NumeroCompte ?? string.Empty).Trim(),
            LibelleCompte = (raw.LibelleCompte ?? string.Empty).Trim(),
            FK_Banque = banque.IdBanque,
            FK_Direction = direction.IdDirection,
            FK_TypeCompte = type.Code,
            FK_Devise = devise.IdDevise,
            FK_Province = province?.IdProvince,
            FK_Utilisateur = null,
            DateCreation = DateTime.SpecifyKind(dateCreation, DateTimeKind.Utc),
            DateCloture = cloture,
            Actif = actif,
        };
    }

    private static ImportComptesResumeDto Resume(IReadOnlyList<ImportCompteLigneDto> lignes)
        => new(
            lignes.Count,
            lignes.Count(l => l.Statut == "a_importer"),
            lignes.Count(l => l.Statut == "doublon_fichier"),
            lignes.Count(l => l.Statut == "deja_existant"),
            lignes.Count(l => l.Statut == "erreur"));

    private static (bool Actif, DateOnly? DateCloture) NormaliserActif(bool actif, DateOnly? clotureActuelle)
    {
        if (actif)
            return (true, null);
        return (false, clotureActuelle ?? DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }

    private static string ValiderNumero(string? brut)
    {
        var numero = (brut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("Le numéro de compte est obligatoire.");
        if (numero.Length > MaxNumero)
            throw new ArgumentException($"Le numéro de compte ne peut pas dépasser {MaxNumero} caractères.");
        return numero;
    }

    private static string ValiderLibelle(string? brut)
    {
        var libelle = (brut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (libelle.Length > MaxLibelle)
            throw new ArgumentException($"Le libellé ne peut pas dépasser {MaxLibelle} caractères.");
        return libelle;
    }

    private static string Key(string banque, string numero)
        => $"{banque.Trim()}|{numero.Trim()}";

    private static string UtilisateurLibelle(Utilisateur? u)
    {
        if (u is null) return string.Empty;
        var nom = string.Join(" ", new[] { u.Prenom, u.Nom }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return nom.Length > 0 ? nom : u.NomUtilisateur;
    }

    private static CompteFinancierDto Map(CompteFinancier c)
        => new(
            c.IdCompte,
            c.NumeroCompte,
            c.LibelleCompte,
            c.FK_Banque,
            c.Banque?.LibelleBanque ?? c.FK_Banque,
            c.FK_Direction,
            c.Direction?.Libelle ?? string.Empty,
            c.FK_TypeCompte,
            c.TypeCompte?.Libelle ?? c.FK_TypeCompte,
            c.FK_Devise,
            c.Devise?.Code ?? string.Empty,
            c.Devise?.Libelle ?? string.Empty,
            c.FK_Province,
            c.Province?.Libelle,
            c.FK_Utilisateur,
            UtilisateurLibelle(c.Utilisateur),
            c.DateCreation,
            c.DateCloture,
            c.DateModification,
            c.Actif);

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

    private sealed record Refs(
        Banque Banque,
        DirectionTresorerie Direction,
        TypeCompte Type,
        Devise Devise,
        Province? Province,
        Utilisateur? Utilisateur);

    private sealed class ImportContext(
        IReadOnlyList<Banque> banques,
        IReadOnlyList<DirectionTresorerie> directions,
        IReadOnlyList<TypeCompte> types,
        IReadOnlyList<Devise> devises,
        IReadOnlyList<Province> provinces,
        HashSet<long> existingIds,
        HashSet<string> existingNumeros)
    {
        public IReadOnlyList<Banque> Banques { get; } = banques;
        public IReadOnlyList<DirectionTresorerie> Directions { get; } = directions;
        public IReadOnlyList<TypeCompte> Types { get; } = types;
        public IReadOnlyList<Devise> Devises { get; } = devises;
        public IReadOnlyList<Province> Provinces { get; } = provinces;
        public HashSet<long> ExistingIds { get; } = existingIds;
        public HashSet<string> ExistingNumeros { get; } = existingNumeros;
    }
}
