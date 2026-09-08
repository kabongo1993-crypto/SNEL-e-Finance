using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class ProvinceService : IProvinceService
{
    private const string IdUtiliseMessage =
        "Cette province est utilisée par un ou plusieurs comptes financiers. Son identifiant ne peut pas être modifié.";

    private const int MaxId = 20;
    private const int MaxLibelle = 200;

    private readonly IProvinceRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public ProvinceService(IProvinceRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProvinceDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProvinceDto?> GetByIdAsync(string idProvince, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync((idProvince ?? string.Empty).Trim(), cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ProvinceDto> CreateAsync(
        CreateProvinceRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var id = ValiderId(request.IdProvince);
        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.IdExistsAsync(id, null, cancellationToken))
            throw new InvalidOperationException($"La province « {id} » existe déjà.");
        if (await _repository.LibelleExistsAsync(libelle, null, cancellationToken))
            throw new InvalidOperationException("Une province portant ce libellé existe déjà.");

        var created = await _repository.AddAsync(
            new Province
            {
                IdProvince = id,
                Libelle = libelle,
                Actif = request.Actif,
                DateCreation = DateTime.UtcNow,
                DateModification = null,
            },
            cancellationToken);
        return Map(created);
    }

    public async Task<ProvinceDto?> UpdateAsync(
        string idProvince,
        UpdateProvinceRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var currentId = (idProvince ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(currentId))
            return null;

        var entity = await _repository.GetByIdAsync(currentId, cancellationToken);
        if (entity is null)
            return null;

        var id = ValiderId(request.IdProvince);
        var libelle = ValiderLibelle(request.Libelle);
        var changementId = !string.Equals(entity.IdProvince, id, StringComparison.Ordinal);

        if (changementId)
        {
            var nbComptes = await _repository.CountComptesByIdAsync(entity.IdProvince, cancellationToken);
            if (nbComptes > 0)
                throw new InvalidOperationException(IdUtiliseMessage);

            if (!string.Equals(entity.IdProvince, id, StringComparison.OrdinalIgnoreCase)
                && await _repository.IdExistsAsync(id, entity.IdProvince, cancellationToken))
                throw new InvalidOperationException($"La province « {id} » existe déjà.");
        }
        else if (await _repository.IdExistsAsync(id, entity.IdProvince, cancellationToken))
        {
            throw new InvalidOperationException($"La province « {id} » existe déjà.");
        }

        if (await _repository.LibelleExistsAsync(libelle, entity.IdProvince, cancellationToken))
            throw new InvalidOperationException("Une province portant ce libellé existe déjà.");

        entity.Libelle = libelle;
        entity.Actif = request.Actif;
        entity.DateModification = DateTime.UtcNow;

        if (changementId)
        {
            await _repository.RenameIdAsync(currentId, id, entity, cancellationToken);
            var reloaded = await _repository.GetByIdAsync(id, cancellationToken);
            if (reloaded is null)
                throw new InvalidOperationException("La province à modifier est introuvable.");
            return Map(reloaded);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
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

    private static string ValiderId(string? idBrut)
    {
        var id = (idBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("L'identifiant province est obligatoire.");
        if (id.Length > MaxId)
            throw new ArgumentException($"L'identifiant province ne peut pas dépasser {MaxId} caractères.");
        return id;
    }

    private static string ValiderLibelle(string? libelleBrut)
    {
        var libelle = (libelleBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (libelle.Length > MaxLibelle)
            throw new ArgumentException($"Le libellé ne peut pas dépasser {MaxLibelle} caractères.");
        return libelle;
    }

    private static ProvinceDto Map(Province p)
        => new(p.IdProvince, p.Libelle, p.Actif, p.DateCreation, p.DateModification);
}
