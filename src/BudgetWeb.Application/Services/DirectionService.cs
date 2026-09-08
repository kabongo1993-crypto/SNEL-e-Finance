using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class DirectionService : IDirectionService
{
    private const int MaxLibelle = 200;

    private readonly IDirectionRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public DirectionService(IDirectionRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DirectionDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<DirectionDto?> GetByIdAsync(long idDirection, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idDirection, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<DirectionDto> CreateAsync(
        CreateDirectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.LibelleExistsAsync(libelle, null, cancellationToken))
            throw new InvalidOperationException("Une direction portant ce libellé existe déjà.");

        var created = await _repository.AddAsync(
            new DirectionTresorerie
            {
                Libelle = libelle,
                Actif = request.Actif,
                DateCreation = DateTime.UtcNow,
                DateModification = null,
            },
            cancellationToken);
        return Map(created);
    }

    public async Task<DirectionDto?> UpdateAsync(
        long idDirection,
        UpdateDirectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idDirection, cancellationToken);
        if (entity is null)
            return null;

        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.LibelleExistsAsync(libelle, idDirection, cancellationToken))
            throw new InvalidOperationException("Une direction portant ce libellé existe déjà.");

        entity.Libelle = libelle;
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

    private static DirectionDto Map(DirectionTresorerie d)
        => new(d.IdDirection, d.Libelle, d.Actif, d.DateCreation, d.DateModification);
}
