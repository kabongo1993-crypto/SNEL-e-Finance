using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class DeviseService : IDeviseService
{
    private readonly IDeviseRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public DeviseService(IDeviseRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DeviseDto>> ListAsync(
        bool actifsSeulement = true,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<DeviseDto?> GetByIdAsync(long idDevise, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idDevise, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<DeviseDto> CreateAsync(CreateDeviseRequest request, CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var code = DemandePaiementMontants.NormaliserCodeDevise(request.Code);
        DemandePaiementMontants.ValiderDevise(code);
        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");

        if (await _repository.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException($"Le code devise « {code} » existe déjà.");

        var entity = new Devise
        {
            Code = code,
            Libelle = libelle,
            Symbole = string.IsNullOrWhiteSpace(request.Symbole) ? null : request.Symbole.Trim(),
            Actif = request.Actif,
        };
        var created = await _repository.AddAsync(entity, cancellationToken);
        return Map(created);
    }

    public async Task<DeviseDto?> UpdateAsync(
        long idDevise,
        UpdateDeviseRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idDevise, cancellationToken);
        if (entity is null)
            return null;

        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");

        if (!request.Actif && entity.Actif)
        {
            // Autorisé même si utilisée (historique).
        }

        entity.Libelle = libelle;
        entity.Symbole = string.IsNullOrWhiteSpace(request.Symbole) ? null : request.Symbole.Trim();
        entity.Actif = request.Actif;
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

    private static DeviseDto Map(Devise d)
        => new(d.IdDevise, d.Code, d.Libelle, d.Symbole, d.Actif);
}
