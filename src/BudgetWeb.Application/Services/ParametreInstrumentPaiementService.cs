using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class ParametreInstrumentPaiementService : IParametreInstrumentPaiementService
{
    private readonly IDemandePaiementRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public ParametreInstrumentPaiementService(
        IDemandePaiementRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ParametreInstrumentPaiementDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = new List<ParametreInstrumentPaiementDto>(TypeInstrumentPaiement.Valeurs.Count);
        foreach (var type in TypeInstrumentPaiement.Valeurs)
        {
            var row = await _repository.GetParametreInstrumentByTypeAsync(type, cancellationToken);
            rows.Add(Map(type, row));
        }

        return rows;
    }

    public async Task<ParametreInstrumentPaiementDto?> GetByTypeAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);
        if (!TypeInstrumentPaiement.IsValid(type))
            throw new ArgumentException("Type d'instrument invalide.");

        var row = await _repository.GetParametreInstrumentByTypeAsync(type, cancellationToken);
        return Map(type, row);
    }

    public async Task<ParametreInstrumentPaiementDto> UpsertAsync(
        string typeInstrument,
        UpsertParametreInstrumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var type = TypeInstrumentPaiement.Normaliser(typeInstrument);
        if (!TypeInstrumentPaiement.IsValid(type))
            throw new ArgumentException("Type d'instrument invalide.");

        var userId = _currentUser.RequireUserId();
        var entity = MapRequest(type, request, userId);

        if (entity.Actif)
            ParametreInstrumentPaiementRules.ExigerParametreComplet(entity, type);

        await _repository.UpsertParametreInstrumentAsync(entity, cancellationToken);

        var saved = await _repository.GetParametreInstrumentByTypeAsync(type, cancellationToken)
            ?? entity;
        return Map(type, saved);
    }

    private static ParametreInstrumentPaiement MapRequest(
        string type,
        UpsertParametreInstrumentRequest request,
        long userId)
        => new()
        {
            TypeInstrument = type,
            Sr = TrimOrNull(request.Sr),
            ComptabiliteGenerale = TrimOrNull(request.ComptabiliteGenerale),
            Cp = TrimOrNull(request.Cp),
            Cpa = TrimOrNull(request.Cpa),
            CompteGeneral = TrimOrNull(request.CompteGeneral),
            CompteParticulier = TrimOrNull(request.CompteParticulier),
            CpCa = TrimOrNull(request.CpCa),
            Ls = TrimOrNull(request.Ls),
            SuiviExtraComptable = TrimOrNull(request.SuiviExtraComptable),
            MontantSuiviExtraComptable = request.MontantSuiviExtraComptable,
            NumeroAppariement = TrimOrNull(request.NumeroAppariement),
            RecuInstitutionnel = TrimOrNull(request.RecuInstitutionnel),
            Actif = request.Actif,
            FK_UtilisateurCreation = userId,
            FK_UtilisateurModification = userId,
        };

    private static ParametreInstrumentPaiementDto Map(string type, ParametreInstrumentPaiement? row)
        => new(
            row?.IdParametreInstrumentPaiement,
            type,
            row?.Sr,
            row?.ComptabiliteGenerale,
            row?.Cp,
            row?.Cpa,
            row?.CompteGeneral,
            row?.CompteParticulier,
            row?.CpCa,
            row?.Ls,
            row?.SuiviExtraComptable,
            row?.MontantSuiviExtraComptable,
            row?.NumeroAppariement,
            row?.RecuInstitutionnel,
            row?.Actif ?? false,
            ParametreInstrumentPaiementRules.EstComplet(row, type));

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private void ExigerLecture()
    {
        if (_currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : referentiels.ecrire ou paiements.charge_dpm.");
    }

    private void ExigerEcriture()
    {
        if (_currentUser.HasPermission(AppPermissions.ReferentielsEcrire)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : referentiels.ecrire.");
    }
}
