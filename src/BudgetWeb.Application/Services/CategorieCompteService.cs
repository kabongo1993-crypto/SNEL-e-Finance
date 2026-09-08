using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class CategorieCompteService : ICategorieCompteService
{
    private const int MaxLibelle = 200;
    private const int MaxOrientation = 200;

    private readonly ICategorieCompteRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public CategorieCompteService(ICategorieCompteRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CategorieCompteDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<CategorieCompteDto?> GetByIdAsync(
        long idCategorieCompte,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idCategorieCompte, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<CategorieCompteDto> CreateAsync(
        CreateCategorieCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var libelle = ValiderLibelle(request.Libelle);
        var orientation = ValiderOrientation(request.Orientation);
        if (await _repository.LibelleExistsAsync(libelle, null, cancellationToken))
            throw new InvalidOperationException("Une catégorie de compte portant ce libellé existe déjà.");

        var created = await _repository.AddAsync(
            new CategorieCompte
            {
                Libelle = libelle,
                Orientation = orientation,
                Actif = request.Actif,
                DateCreation = DateTime.UtcNow,
                DateModification = null,
            },
            cancellationToken);
        return Map(created);
    }

    public async Task<CategorieCompteDto?> UpdateAsync(
        long idCategorieCompte,
        UpdateCategorieCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idCategorieCompte, cancellationToken);
        if (entity is null)
            return null;

        var libelle = ValiderLibelle(request.Libelle);
        var orientation = ValiderOrientation(request.Orientation);
        if (await _repository.LibelleExistsAsync(libelle, idCategorieCompte, cancellationToken))
            throw new InvalidOperationException("Une catégorie de compte portant ce libellé existe déjà.");

        entity.Libelle = libelle;
        entity.Orientation = orientation;
        entity.Actif = request.Actif;
        entity.DateModification = DateTime.UtcNow;
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

    private static string ValiderLibelle(string? libelleBrut)
    {
        var libelle = (libelleBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (libelle.Length > MaxLibelle)
            throw new ArgumentException($"Le libellé ne peut pas dépasser {MaxLibelle} caractères.");
        return libelle;
    }

    private static string? ValiderOrientation(string? orientationBrut)
    {
        var orientation = (orientationBrut ?? string.Empty).Trim();
        if (orientation.Length == 0)
            return null;
        if (orientation.Length > MaxOrientation)
            throw new ArgumentException($"L'orientation ne peut pas dépasser {MaxOrientation} caractères.");
        return orientation;
    }

    private static CategorieCompteDto Map(CategorieCompte c)
        => new(c.IdCategorieCompte, c.Libelle, c.Orientation, c.Actif, c.DateCreation, c.DateModification);
}
