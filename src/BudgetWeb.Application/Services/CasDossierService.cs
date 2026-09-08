using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Security;

namespace BudgetWeb.Application.Services;

public sealed class CasDossierService : ICasDossierService
{
    private readonly ICasDossierRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public CasDossierService(ICasDossierRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CasDossierDto>> ListAsync(
        bool actifsSeulement = true,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var rows = await _repository.ListAsync(actifsSeulement ? true : null, cancellationToken);
        return rows.Select(c => Map(c, piecesActivesSeulement: actifsSeulement)).ToList();
    }

    public async Task<CasDossierDto?> GetByIdAsync(
        long idCasDossier,
        bool piecesActivesSeulement = true,
        CancellationToken cancellationToken = default)
    {
        ExigerLecture();
        var row = await _repository.GetWithPiecesAsync(idCasDossier, cancellationToken);
        return row is null ? null : Map(row, piecesActivesSeulement);
    }

    public async Task<CasDossierDto> CreateAsync(CreateCasDossierRequest request, CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Le code est obligatoire.");
        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (request.Ordre < 1)
            throw new ArgumentException("L'ordre doit être supérieur ou égal à 1.");

        if (await _repository.CodeExistsAsync(code, null, cancellationToken))
            throw new InvalidOperationException($"Le code cas de dossier « {code} » existe déjà.");

        var entity = new CasDossier
        {
            Code = code,
            Libelle = libelle,
            Ordre = request.Ordre,
            Actif = request.Actif,
        };
        var created = await _repository.AddAsync(entity, cancellationToken);
        return Map(created, piecesActivesSeulement: false);
    }

    public async Task<CasDossierDto?> UpdateAsync(
        long idCasDossier,
        UpdateCasDossierRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var entity = await _repository.GetByIdAsync(idCasDossier, cancellationToken);
        if (entity is null)
            return null;

        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (request.Ordre < 1)
            throw new ArgumentException("L'ordre doit être supérieur ou égal à 1.");

        entity.Libelle = libelle;
        entity.Ordre = request.Ordre;
        entity.Actif = request.Actif;
        await _repository.SaveChangesAsync(cancellationToken);

        var refreshed = await _repository.GetWithPiecesAsync(idCasDossier, cancellationToken)
            ?? entity;
        return Map(refreshed, piecesActivesSeulement: false);
    }

    public async Task<CasDossierPieceObligatoireDto> AddPieceAsync(
        long idCasDossier,
        CreateCasDossierPieceRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var cas = await _repository.GetByIdAsync(idCasDossier, cancellationToken)
            ?? throw new InvalidOperationException("Cas de dossier introuvable.");

        var code = (request.CodeTypePiece ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Le code type pièce est obligatoire.");
        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (request.Ordre < 1)
            throw new ArgumentException("L'ordre doit être supérieur ou égal à 1.");

        ValiderActifObligatoire(request.Actif, request.Obligatoire);

        if (await _repository.PieceCodeExistsAsync(idCasDossier, code, null, cancellationToken))
            throw new InvalidOperationException($"Le type de pièce « {code} » existe déjà pour ce cas.");

        var piece = new CasDossierPieceObligatoire
        {
            FK_CasDossier = cas.IdCasDossier,
            CodeTypePiece = code,
            Libelle = libelle,
            Ordre = request.Ordre,
            Actif = request.Actif,
            Obligatoire = request.Obligatoire,
        };
        await _repository.AddPieceAsync(piece, cancellationToken);
        return MapPiece(piece);
    }

    public async Task<CasDossierPieceObligatoireDto?> UpdatePieceAsync(
        long idCasDossier,
        long idPiece,
        UpdateCasDossierPieceRequest request,
        CancellationToken cancellationToken = default)
    {
        ExigerEcriture();
        var piece = await _repository.GetPieceTrackedAsync(idPiece, cancellationToken);
        if (piece is null || piece.FK_CasDossier != idCasDossier)
            return null;

        var libelle = (request.Libelle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé est obligatoire.");
        if (request.Ordre < 1)
            throw new ArgumentException("L'ordre doit être supérieur ou égal à 1.");

        var actif = request.Actif;
        var obligatoire = request.Obligatoire;
        if (!actif)
            obligatoire = false;

        ValiderActifObligatoire(actif, obligatoire);

        piece.Libelle = libelle;
        piece.Ordre = request.Ordre;
        piece.Actif = actif;
        piece.Obligatoire = obligatoire;
        await _repository.SaveChangesAsync(cancellationToken);
        return MapPiece(piece);
    }

    private static void ValiderActifObligatoire(bool actif, bool obligatoire)
    {
        if (!actif && obligatoire)
            throw new InvalidOperationException("Une pièce désactivée ne peut pas être obligatoire.");
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

    private static CasDossierDto Map(CasDossier cas, bool piecesActivesSeulement)
    {
        var pieces = cas.PiecesObligatoires.AsEnumerable();
        if (piecesActivesSeulement)
            pieces = pieces.Where(p => p.Actif);

        return new CasDossierDto(
            cas.IdCasDossier,
            cas.Code,
            cas.Libelle,
            cas.Ordre,
            cas.Actif,
            pieces.OrderBy(p => p.Ordre).Select(MapPiece).ToList());
    }

    private static CasDossierPieceObligatoireDto MapPiece(CasDossierPieceObligatoire piece)
        => new(
            piece.IdPieceObligatoire,
            piece.CodeTypePiece,
            piece.Libelle,
            piece.Ordre,
            piece.Actif,
            piece.Obligatoire);
}
