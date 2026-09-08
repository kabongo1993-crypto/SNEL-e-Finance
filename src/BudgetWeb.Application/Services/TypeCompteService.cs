using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class TypeCompteService : ITypeCompteService
{
    private const string CodeUtiliseMessage =
        "Ce type de compte est utilisé par un ou plusieurs comptes financiers. Son code ne peut pas être modifié.";

    private const int MaxCode = 20;
    private const int MaxLibelle = 200;

    private readonly ITypeCompteRepository _repository;
    private readonly IGroupeTypeCompteRepository _groupes;
    private readonly ICurrentUserService _currentUser;

    public TypeCompteService(
        ITypeCompteRepository repository,
        IGroupeTypeCompteRepository groupes,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _groupes = groupes;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TypeCompteDto>> ListAsync(
        bool actifsSeulement = false,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<TypeCompteDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetByCodeAsync((code ?? string.Empty).Trim(), cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<TypeCompteDto> CreateAsync(
        CreateTypeCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var code = ValiderCode(request.Code);
        var libelle = ValiderLibelle(request.Libelle);
        if (await _repository.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException($"Un type de compte portant le code « {code} » existe déjà.");

        var groupe = await ExigerGroupeAsync(request.IdGroupeTypeCompte, exigerActif: true, cancellationToken);

        var created = await _repository.AddAsync(
            new TypeCompte
            {
                Code = code,
                Libelle = libelle,
                FK_GroupeTypeCompte = groupe.IdGroupeTypeCompte,
                Actif = request.Actif,
                DateCreation = DateTime.UtcNow,
                DateModification = null,
            },
            cancellationToken);
        created.GroupeTypeCompte = groupe;
        return Map(created);
    }

    public async Task<TypeCompteDto?> UpdateAsync(
        string codeActuel,
        UpdateTypeCompteRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var currentCode = (codeActuel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(currentCode))
            return null;

        var entity = await _repository.GetByCodeAsync(currentCode, cancellationToken);
        if (entity is null)
            return null;

        var code = ValiderCode(request.Code);
        var libelle = ValiderLibelle(request.Libelle);
        var changementCode = !string.Equals(entity.Code, code, StringComparison.Ordinal);

        if (changementCode)
        {
            var nbComptes = await _repository.CountComptesByCodeAsync(entity.Code, cancellationToken);
            if (nbComptes > 0)
                throw new InvalidOperationException(CodeUtiliseMessage);

            if (!string.Equals(entity.Code, code, StringComparison.OrdinalIgnoreCase)
                && await _repository.CodeExistsAsync(code, entity.Code, cancellationToken))
                throw new InvalidOperationException($"Un type de compte portant le code « {code} » existe déjà.");
        }
        else if (await _repository.CodeExistsAsync(code, entity.Code, cancellationToken))
        {
            throw new InvalidOperationException($"Un type de compte portant le code « {code} » existe déjà.");
        }

        var changementGroupe = entity.FK_GroupeTypeCompte != request.IdGroupeTypeCompte;
        var groupe = await ExigerGroupeAsync(
            request.IdGroupeTypeCompte,
            exigerActif: changementGroupe,
            cancellationToken);

        entity.Libelle = libelle;
        entity.FK_GroupeTypeCompte = groupe.IdGroupeTypeCompte;
        entity.Actif = request.Actif;
        entity.DateModification = DateTime.UtcNow;
        entity.GroupeTypeCompte = groupe;

        if (changementCode)
        {
            await _repository.RenameCodeAsync(currentCode, code, entity, cancellationToken);
            var reloaded = await _repository.GetByCodeAsync(code, cancellationToken);
            if (reloaded is null)
                throw new InvalidOperationException("Le type de compte à modifier est introuvable.");
            reloaded.GroupeTypeCompte = groupe;
            return Map(reloaded);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private async Task<GroupeTypeCompte> ExigerGroupeAsync(
        long idGroupeTypeCompte,
        bool exigerActif,
        CancellationToken cancellationToken)
    {
        if (idGroupeTypeCompte <= 0)
            throw new ArgumentException("Le groupe de types de comptes est obligatoire.");

        var groupe = await _groupes.GetByIdAsync(idGroupeTypeCompte, cancellationToken)
            ?? throw new InvalidOperationException("Le groupe de types de comptes indiqué est introuvable.");

        if (exigerActif && !groupe.Actif)
            throw new InvalidOperationException("Le groupe de types de comptes indiqué est inactif.");

        return groupe;
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

    private static string ValiderCode(string? codeBrut)
    {
        var code = (codeBrut ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Le code est obligatoire.");
        if (code.Length > MaxCode)
            throw new ArgumentException($"Le code ne peut pas dépasser {MaxCode} caractères.");
        return code;
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

    private static TypeCompteDto Map(TypeCompte t)
        => new(
            t.Code,
            t.Libelle,
            t.FK_GroupeTypeCompte,
            t.GroupeTypeCompte?.Libelle ?? string.Empty,
            t.Actif,
            t.DateCreation,
            t.DateModification);
}
