using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class GroupeTypeCompteService : IGroupeTypeCompteService
{
    private const int MaxLibelle = 200;

    private readonly IGroupeTypeCompteRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GroupeTypeCompteService(IGroupeTypeCompteRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<GroupeTypeCompteDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<GroupeTypeCompteDto?> GetByIdAsync(
        long idGroupeTypeCompte,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idGroupeTypeCompte, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<GroupeTypeCompteDto> CreateAsync(
        CreateGroupeTypeCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.LibelleExistsAsync(libelle, null, cancellationToken))
            throw new InvalidOperationException("Un groupe de types de comptes portant ce libellé existe déjà.");

        var created = await _repository.AddAsync(
            new GroupeTypeCompte
            {
                Libelle = libelle,
                Actif = request.Actif,
                DateCreation = DateTime.UtcNow,
                DateModification = null,
            },
            cancellationToken);
        return Map(created);
    }

    public async Task<GroupeTypeCompteDto?> UpdateAsync(
        long idGroupeTypeCompte,
        UpdateGroupeTypeCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idGroupeTypeCompte, cancellationToken);
        if (entity is null)
            return null;

        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.LibelleExistsAsync(libelle, idGroupeTypeCompte, cancellationToken))
            throw new InvalidOperationException("Un groupe de types de comptes portant ce libellé existe déjà.");

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

    private static GroupeTypeCompteDto Map(GroupeTypeCompte g)
        => new(g.IdGroupeTypeCompte, g.Libelle, g.Actif, g.DateCreation, g.DateModification);
}
