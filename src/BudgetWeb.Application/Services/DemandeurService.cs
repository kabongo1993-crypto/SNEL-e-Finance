using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class DemandeurService : IDemandeurService
{
    private readonly IDemandeurRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPerimetreAccesService _perimetreAcces;

    public DemandeurService(
        IDemandeurRepository repository,
        ICurrentUserService currentUser,
        IPerimetreAccesService perimetreAcces)
    {
        _repository = repository;
        _currentUser = currentUser;
        _perimetreAcces = perimetreAcces;
    }

    public async Task<IReadOnlyList<DemandeurDto>> ListAsync(
        bool actifsSeulement = true,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);

        if (_perimetreAcces.PeutVoirToutesUbDpm())
            return rows.Select(Map).ToList();

        var filtered = new List<Demandeur>();
        foreach (var row in rows)
        {
            if (await _perimetreAcces.PeutAccederUbDpmAsync(row.FK_UniteBudgetaire, cancellationToken))
                filtered.Add(row);
        }

        return filtered.Select(Map).ToList();
    }

    public async Task<DemandeurDto?> GetByIdAsync(long idDemandeur, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByIdAsync(idDemandeur, cancellationToken);
        if (row is null)
            return null;

        await GarantirAccesDemandeurAsync(row.FK_UniteBudgetaire, cancellationToken);
        return Map(row);
    }

    public async Task<DemandeurDto> CreateAsync(
        CreateDemandeurRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var userId = _currentUser.RequireUserId();
        var code = DemandeurRules.NormaliserCode(request.Code);
        var libelle = DemandeurRules.NormaliserLibelle(request.Libelle);
        DemandeurRules.ExigerUb(request.IdUB);

        if (await _repository.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException($"Le code demandeur « {code} » existe déjà.");

        var ub = await _repository.GetUniteBudgetaireAsync(request.IdUB, cancellationToken)
            ?? throw new InvalidOperationException("Unité budgétaire introuvable.");

        if (!ub.Actif)
            throw new InvalidOperationException("L'unité budgétaire est inactive.");

        await GarantirAccesDemandeurAsync(ub.IdUB, cancellationToken);

        var entity = new Demandeur
        {
            Code = code,
            Libelle = libelle,
            FK_UniteBudgetaire = ub.IdUB,
            Actif = true,
            DateCreation = DateTime.UtcNow,
            FK_UtilisateurCreation = userId,
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        var reloaded = await _repository.GetByIdAsync(created.IdDemandeur, cancellationToken)
            ?? throw new InvalidOperationException("Demandeur introuvable après création.");
        return Map(reloaded);
    }

    public async Task<DemandeurDto?> UpdateAsync(
        long idDemandeur,
        UpdateDemandeurRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var userId = _currentUser.RequireUserId();
        var entity = await _repository.GetTrackedAsync(idDemandeur, cancellationToken);
        if (entity is null)
            return null;

        await GarantirAccesDemandeurAsync(entity.FK_UniteBudgetaire, cancellationToken);

        var code = DemandeurRules.NormaliserCode(request.Code);
        var libelle = DemandeurRules.NormaliserLibelle(request.Libelle);
        DemandeurRules.ExigerUb(request.IdUB);

        if (await _repository.CodeExistsAsync(code, idDemandeur, cancellationToken))
            throw new InvalidOperationException($"Le code demandeur « {code} » existe déjà.");

        var ub = await _repository.GetUniteBudgetaireAsync(request.IdUB, cancellationToken)
            ?? throw new InvalidOperationException("Unité budgétaire introuvable.");

        await GarantirAccesDemandeurAsync(ub.IdUB, cancellationToken);

        entity.Code = code;
        entity.Libelle = libelle;
        entity.FK_UniteBudgetaire = ub.IdUB;
        entity.Actif = request.Actif;
        entity.DateModification = DateTime.UtcNow;
        entity.FK_UtilisateurModification = userId;

        await _repository.SaveChangesAsync(cancellationToken);

        var reloaded = await _repository.GetByIdAsync(idDemandeur, cancellationToken);
        return reloaded is null ? null : Map(reloaded);
    }

    private void ExigerLecture()
    {
        if (_currentUser.HasPermission(AppPermissions.PaiementsLire)
            || _currentUser.HasPermission(AppPermissions.PaiementsEcrire)
            || _currentUser.HasPermission(AppPermissions.PaiementsSoumettre)
            || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
            || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget)
            || _currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : paiements.lire.");
    }

    private void ExigerEcriture()
    {
        if (_currentUser.HasPermission(AppPermissions.DemandeursEcrire)
            || _currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : demandeurs.ecrire.");
    }

    private Task GarantirAccesDemandeurAsync(long idUB, CancellationToken cancellationToken)
        => _perimetreAcces.GarantirAccesUbDpmAsync(idUB, cancellationToken);

    private static DemandeurDto Map(Demandeur d)
    {
        var ub = d.UniteBudgetaire
            ?? throw new InvalidOperationException("UB manquante sur le demandeur.");
        var dept = ub.Departement;
        return new DemandeurDto(
            d.IdDemandeur,
            d.Code,
            d.Libelle,
            ub.IdUB,
            ub.CodeUB,
            ub.Libelle,
            dept?.IdDepartement ?? ub.FK_Departement,
            dept?.Code ?? string.Empty,
            dept?.Libelle ?? string.Empty,
            d.Actif,
            d.DateCreation,
            d.DateModification);
    }
}
