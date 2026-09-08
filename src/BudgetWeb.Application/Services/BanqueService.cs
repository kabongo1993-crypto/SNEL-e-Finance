using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class BanqueService : IBanqueService
{
    private const int MaxId = 50;
    private const int MaxLibelle = 200;
    private const int MaxPays = 100;

    private readonly IBanqueRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public BanqueService(IBanqueRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<BanqueDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<BanqueDto?> GetByIdAsync(string idBanque, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idBanque.Trim(), cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<BanqueDto> CreateAsync(CreateBanqueRequest request, CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var (id, libelle, pays) = Valider(request.IdBanque, request.LibelleBanque, request.Pays);
        if (await _repository.ExistsAsync(id, cancellationToken))
            throw new InvalidOperationException($"La banque « {id} » existe déjà.");

        var now = DateTime.UtcNow;
        var created = await _repository.AddAsync(
            new Banque
            {
                IdBanque = id,
                LibelleBanque = libelle,
                Pays = pays,
                Actif = request.Actif,
                DateCreation = now,
                DateModification = null,
            },
            cancellationToken);
        return Map(created);
    }

    public async Task<BanqueDto?> UpdateAsync(
        string idBanque,
        UpdateBanqueRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idBanque.Trim(), cancellationToken);
        if (entity is null)
            return null;

        var (_, libelle, pays) = Valider(entity.IdBanque, request.LibelleBanque, request.Pays);
        entity.LibelleBanque = libelle;
        entity.Pays = pays;
        entity.Actif = request.Actif;
        entity.DateModification = DateTime.UtcNow;
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ImportBanquesResultDto> ImportAsync(
        ImportBanquesRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var items = request.Banques ?? Array.Empty<ImportBanqueItemRequest>();
        var details = new List<ImportBanqueDetailDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = new HashSet<string>(
            await _repository.ListIdsAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<Banque>();
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            string id;
            string libelle;
            string? pays;
            try
            {
                (id, libelle, pays) = Valider(item.IdBanque, item.LibelleBanque, item.Pays);
            }
            catch (ArgumentException ex)
            {
                details.Add(new ImportBanqueDetailDto(item.IdBanque?.Trim() ?? string.Empty, ex.Message));
                continue;
            }

            if (!seen.Add(id))
            {
                details.Add(new ImportBanqueDetailDto(id, "Doublon dans le lot d'import."));
                continue;
            }

            if (existing.Contains(id))
            {
                details.Add(new ImportBanqueDetailDto(id, "Déjà existante."));
                continue;
            }

            toInsert.Add(new Banque
            {
                IdBanque = id,
                LibelleBanque = libelle,
                Pays = pays,
                Actif = true,
                DateCreation = now,
                DateModification = null,
            });
            existing.Add(id);
        }

        if (toInsert.Count > 0)
            await _repository.AddRangeInTransactionAsync(toInsert, cancellationToken);

        var erreurs = details.Count(d =>
            !d.Motif.Equals("Déjà existante.", StringComparison.Ordinal));
        var deja = details.Count(d =>
            d.Motif.Equals("Déjà existante.", StringComparison.Ordinal));

        return new ImportBanquesResultDto(
            items.Count,
            toInsert.Count,
            deja,
            erreurs,
            details);
    }

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

    private static (string Id, string Libelle, string? Pays) Valider(
        string? idBrut,
        string? libelleBrut,
        string? paysBrut)
    {
        var id = (idBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("L'ID Banque est obligatoire.");
        if (id.Length > MaxId)
            throw new ArgumentException($"L'ID Banque ne peut pas dépasser {MaxId} caractères.");

        var libelle = (libelleBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé de la banque est obligatoire.");
        if (libelle.Length > MaxLibelle)
            throw new ArgumentException($"Le libellé ne peut pas dépasser {MaxLibelle} caractères.");

        var pays = string.IsNullOrWhiteSpace(paysBrut) ? null : paysBrut.Trim();
        if (pays is { Length: > MaxPays })
            throw new ArgumentException($"Le pays ne peut pas dépasser {MaxPays} caractères.");

        return (id, libelle, pays);
    }

    private static BanqueDto Map(Banque b)
        => new(b.IdBanque, b.LibelleBanque, b.Pays, b.Actif, b.DateCreation, b.DateModification);
}
