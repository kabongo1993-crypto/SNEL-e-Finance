using System.IO.Compression;
using System.Text;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Security;
using Microsoft.Extensions.Logging;

namespace BudgetWeb.Application.Services;

public sealed class DocumentsEtablisService : IDocumentsEtablisService
{
    public const int PeriodeMaxJours = 366;
    public const int ArchiveMaxDocuments = 80;

    private readonly IDemandePaiementRepository _repository;
    private readonly IDemandePaiementService _demandePaiementService;
    private readonly IDocumentsEtablisListePdfRenderer _listePdfRenderer;
    private readonly IDocumentsEtablisPdfMerger _pdfMerger;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DocumentsEtablisService> _logger;

    public DocumentsEtablisService(
        IDemandePaiementRepository repository,
        IDemandePaiementService demandePaiementService,
        IDocumentsEtablisListePdfRenderer listePdfRenderer,
        IDocumentsEtablisPdfMerger pdfMerger,
        ICurrentUserService currentUser,
        ILogger<DocumentsEtablisService> logger)
    {
        _repository = repository;
        _demandePaiementService = demandePaiementService;
        _listePdfRenderer = listePdfRenderer;
        _pdfMerger = pdfMerger;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentEtabliListItemDto>> ListAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        ExigerChargeDpm();
        ValiderQuery(query);

        if (query.IdUB is long idUb && !PeutVoirToutesUb())
        {
            var userId = _currentUser.RequireUserId();
            if (!await _repository.UtilisateurPeutAccederUbAsync(userId, idUb, cancellationToken))
            {
                throw new UnauthorizedAccessException(
                    "Vous n'avez pas accès à cette unité budgétaire.");
            }
        }

        var rows = await _repository.ListDocumentsEtablisAsync(query, cancellationToken);
        var visibles = await FiltrerPerimetreAsync(rows, cancellationToken);
        return AppliquerSelection(visibles, query.Selection);
    }

    public async Task<byte[]> GenererListePdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(query, cancellationToken);
        return _listePdfRenderer.Render(query, rows);
    }

    public async Task<byte[]> GenererArchivePdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(query, cancellationToken);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun document établi sur cette période.");
        }

        if (rows.Count > ArchiveMaxDocuments)
        {
            throw new InvalidOperationException(
                $"L'archive est limitée à {ArchiveMaxDocuments} documents. Affinez la période ou le type.");
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pdf = await GenererPdfDocumentAsync(row, cancellationToken);
                var entryName = UniqueZipName(usedNames, row);
                var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var stream = entry.Open();
                await stream.WriteAsync(pdf, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Archive documents établis générée ({Count} fichiers, {Debut}–{Fin})",
            rows.Count,
            query.DateDebut,
            query.DateFin);

        return buffer.ToArray();
    }

    public async Task<byte[]> GenererDocumentsPdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(query, cancellationToken);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucun document établi à afficher sur cette période.");
        }

        if (rows.Count > ArchiveMaxDocuments)
        {
            throw new InvalidOperationException(
                $"L'impression groupée est limitée à {ArchiveMaxDocuments} documents. Affinez la période, le type ou la sélection.");
        }

        var pdfs = new List<byte[]>(rows.Count);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pdfs.Add(await GenererPdfDocumentAsync(row, cancellationToken));
        }

        var merged = _pdfMerger.Merge(pdfs);
        _logger.LogInformation(
            "PDF documents établis généré ({Count} documents, {Debut}–{Fin})",
            rows.Count,
            query.DateDebut,
            query.DateFin);
        return merged;
    }

    private async Task<byte[]> GenererPdfDocumentAsync(
        DocumentEtabliListItemDto row,
        CancellationToken cancellationToken)
    {
        var type = TypeDocumentEtabli.Normaliser(row.TypeDocument);
        return type switch
        {
            TypeDocumentEtabli.BilletConversion
                => await _demandePaiementService.GenererBilletConversionPdfAsync(
                    row.IdDemandePaiement, cancellationToken),
            TypeDocumentEtabli.PieceCaisse
                => await _demandePaiementService.GenererPieceCaissePdfAsync(
                    row.IdDemandePaiement, cancellationToken),
            TypeDocumentEtabli.BonProvisoire
                => await _demandePaiementService.GenererBonProvisoirePdfAsync(
                    row.IdDemandePaiement, cancellationToken),
            TypeDocumentEtabli.MinuteCheque
                => await _demandePaiementService.GenererMinuteChequePdfAsync(
                    row.IdDemandePaiement, cancellationToken),
            _ => throw new InvalidOperationException($"Type de document inconnu : {row.TypeDocument}."),
        };
    }

    private async Task<IReadOnlyList<DocumentEtabliListItemDto>> FiltrerPerimetreAsync(
        IReadOnlyList<DocumentEtabliListItemDto> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0 || PeutVoirToutesUb())
            return rows;

        var userId = _currentUser.RequireUserId();
        var allowed = new HashSet<long>();
        foreach (var idUb in rows.Select(r => r.IdUB).Distinct())
        {
            if (await _repository.UtilisateurPeutAccederUbAsync(userId, idUb, cancellationToken))
                allowed.Add(idUb);
        }

        return rows.Where(r => allowed.Contains(r.IdUB)).ToList();
    }

    internal static IReadOnlyList<DocumentEtabliListItemDto> AppliquerSelection(
        IReadOnlyList<DocumentEtabliListItemDto> rows,
        IReadOnlyList<DocumentEtabliSelectionDto>? selection)
    {
        if (selection is not { Count: > 0 })
            return rows;

        var keys = new HashSet<string>(
            selection.Select(s => s.Cle()),
            StringComparer.OrdinalIgnoreCase);
        return rows
            .Where(r => keys.Contains(DocumentEtabliSelectionDto.Cle(
                r.IdDemandePaiement, r.TypeDocument, r.NumeroDocument)))
            .ToList();
    }

    private static void ValiderQuery(DocumentsEtablisQuery query)
    {
        if (query.DateDebut > query.DateFin)
        {
            throw new ArgumentException(
                "La date de début doit précéder la date de fin.",
                nameof(query));
        }

        if (query.DateFin.DayNumber - query.DateDebut.DayNumber > PeriodeMaxJours)
        {
            throw new ArgumentException(
                $"La période ne peut pas dépasser {PeriodeMaxJours} jours.",
                nameof(query));
        }

        if (!string.IsNullOrWhiteSpace(query.TypeDocument)
            && !TypeDocumentEtabli.IsValid(query.TypeDocument))
        {
            throw new ArgumentException(
                "Type de document invalide.",
                nameof(query));
        }
    }

    private void ExigerChargeDpm()
    {
        if (_currentUser.HasPermission(AppPermissions.PaiementsChargeDpm)
            || _currentUser.HasPermission(AppPermissions.PaiementsReceptionBudget)
            || _currentUser.HasPermission(AppPermissions.AdminAll))
            return;

        throw new UnauthorizedAccessException("Permission requise : paiements.charge_dpm.");
    }

    private bool PeutVoirToutesUb()
        => _currentUser.HasPermission(AppPermissions.AdminAll)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerDc)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerAe)
           || _currentUser.HasPermission(AppPermissions.PaiementsImputerBi)
           || _currentUser.HasPermission(AppPermissions.PaiementsControlerBudget)
           || _currentUser.HasPermission(AppPermissions.PaiementsViserBudget);

    private static string UniqueZipName(HashSet<string> used, DocumentEtabliListItemDto row)
    {
        var safeRef = SanitizeFilePart(row.Reference);
        var safeType = SanitizeFilePart(row.TypeDocument);
        var safeNum = SanitizeFilePart(
            string.IsNullOrWhiteSpace(row.NumeroDocument) ? row.IdDemandePaiement.ToString() : row.NumeroDocument);
        var baseName = $"{safeRef}_{safeType}_{safeNum}.pdf";
        var name = baseName;
        var i = 2;
        while (!used.Add(name))
        {
            name = $"{safeRef}_{safeType}_{safeNum}_{i}.pdf";
            i++;
        }

        return name;
    }

    private static string SanitizeFilePart(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        return sb.Length == 0 ? "doc" : sb.ToString();
    }
}
