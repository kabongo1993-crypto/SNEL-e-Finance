using System.Security.Cryptography;
using System.Text.Json;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public static class DocumentReferenceGenerator
{
    public static string Build(
        string codeDepartement,
        short annee,
        int numeroVersion,
        string typeDocument,
        int sequence)
    {
        var dept = (codeDepartement ?? "XXX").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(dept)) dept = "XXX";
        var type = (typeDocument ?? "DOC").Trim().ToUpperInvariant();
        return $"SNEL/{dept}/BUD/{annee}/V{numeroVersion:00}/{type}/{sequence:00000}";
    }
}

public class DocumentPrevisionService : IDocumentPrevisionService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IDocumentPrevisionRepository _repository;
    private readonly IDocumentPdfRenderer _pdfRenderer;
    private readonly IDocumentFileStore _fileStore;

    public DocumentPrevisionService(
        IDocumentPrevisionRepository repository,
        IDocumentPdfRenderer pdfRenderer,
        IDocumentFileStore fileStore)
    {
        _repository = repository;
        _pdfRenderer = pdfRenderer;
        _fileStore = fileStore;
    }

    public async Task<DocumentPrevisionDto> GenererSoumissionUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
    {
        var ctx = await BuildUbContextAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("SOUMETTRE_UB", idUtilisateur, cancellationToken);
        var ligne = new DocumentPrevisionUbLigneDto(
            idUB, ctx.CodeUB, ctx.LibelleUB, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Dc + ctx.Ae + ctx.Bi,
            StatutAvant: StatutVersionBudgetaire.Brouillon, StatutApres: StatutVersionBudgetaire.Soumise);

        var cmd = new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Soumission, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            ctx.IdDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            idUB, ctx.CodeUB, ctx.LibelleUB, DocumentPrevisionPortee.Ub, 1,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            StatutVersionBudgetaire.Brouillon, StatutVersionBudgetaire.Soumise,
            null, null, ctx.Dc, ctx.Ae, ctx.Bi, [ligne]);
        return await CreerAsync(await EnrichUbCommandAsync(cmd, idVersion, idUB, cancellationToken), cancellationToken);
    }

    public async Task<DocumentPrevisionDto?> GenererSoumissionDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
    {
        if (ubTraitees.Count == 0) return null;
        var ctx = await BuildDepartementContextAsync(idVersion, idDepartement, idUtilisateur, ubTraitees, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("SOUMISSION_DEPARTEMENT", idUtilisateur, cancellationToken);
        return await CreerAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Soumission, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            idDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            null, null, null, DocumentPrevisionPortee.Departement, ctx.Lignes.Count,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            null, StatutVersionBudgetaire.Soumise,
            null, null, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Lignes)
        { RoleActeur = "Utilisateur" }, cancellationToken);
    }

    public async Task<DocumentPrevisionDto> GenererRejetUbAsync(
        long idVersion, long idUB, long idUtilisateur, string motif, string? statutAvant,
        long? idAudit, CancellationToken cancellationToken = default)
    {
        var ctx = await BuildUbContextAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("REJET_UB", idUtilisateur, cancellationToken);
        var avant = string.IsNullOrWhiteSpace(statutAvant) ? null : StatutVersionBudgetaire.Normaliser(statutAvant);
        var ligne = new DocumentPrevisionUbLigneDto(
            idUB, ctx.CodeUB, ctx.LibelleUB, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Dc + ctx.Ae + ctx.Bi, avant, StatutVersionBudgetaire.Rejetee);

        return await CreerAsync(await EnrichUbCommandAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Rejet, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            ctx.IdDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            idUB, ctx.CodeUB, ctx.LibelleUB, DocumentPrevisionPortee.Ub, 1,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            avant, StatutVersionBudgetaire.Rejetee,
            motif, null, ctx.Dc, ctx.Ae, ctx.Bi, [ligne]), idVersion, idUB, cancellationToken), cancellationToken);
    }

    public async Task<DocumentPrevisionDto?> GenererRejetDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, string motif, long? idAudit,
        IReadOnlyList<(long IdUB, string? StatutAvant)> ubTraitees, CancellationToken cancellationToken = default)
    {
        if (ubTraitees.Count == 0) return null;
        var ids = ubTraitees.Select(u => u.IdUB).ToList();
        var ctx = await BuildDepartementContextAsync(idVersion, idDepartement, idUtilisateur, ids, cancellationToken);
        var avantMap = ubTraitees.ToDictionary(x => x.IdUB, x => x.StatutAvant);
        var lignes = ctx.Lignes.Select(l => l with
        {
            StatutAvant = avantMap.GetValueOrDefault(l.IdUB),
            StatutApres = StatutVersionBudgetaire.Rejetee,
        }).ToList();
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("REJET_DEPARTEMENT", idUtilisateur, cancellationToken);
        return await CreerAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Rejet, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            idDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            null, null, null, DocumentPrevisionPortee.Departement, lignes.Count,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            null, StatutVersionBudgetaire.Rejetee,
            motif, null, ctx.Dc, ctx.Ae, ctx.Bi, lignes)
        { RoleActeur = "Responsable" }, cancellationToken);
    }

    public async Task<DocumentPrevisionDto> GenererControleUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
    {
        var ctx = await BuildUbContextAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("CONTROLER_UB", idUtilisateur, cancellationToken);
        var ligne = new DocumentPrevisionUbLigneDto(
            idUB, ctx.CodeUB, ctx.LibelleUB, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Dc + ctx.Ae + ctx.Bi,
            StatutVersionBudgetaire.Soumise, StatutVersionBudgetaire.Controlee);
        return await CreerAsync(await EnrichUbCommandAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Controle, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            ctx.IdDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            idUB, ctx.CodeUB, ctx.LibelleUB, DocumentPrevisionPortee.Ub, 1,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            StatutVersionBudgetaire.Soumise, StatutVersionBudgetaire.Controlee,
            null, "Contrôle favorable.", ctx.Dc, ctx.Ae, ctx.Bi, [ligne]), idVersion, idUB, cancellationToken), cancellationToken);
    }

    public async Task<DocumentPrevisionDto?> GenererControleDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
    {
        if (ubTraitees.Count == 0) return null;
        var ctx = await BuildDepartementContextAsync(idVersion, idDepartement, idUtilisateur, ubTraitees, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("CONTROLE_DEPARTEMENT", idUtilisateur, cancellationToken);
        return await CreerAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Controle, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            idDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            null, null, null, DocumentPrevisionPortee.Departement, ctx.Lignes.Count,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            StatutVersionBudgetaire.Soumise, StatutVersionBudgetaire.Controlee,
            null, "Contrôle favorable.", ctx.Dc, ctx.Ae, ctx.Bi, ctx.Lignes)
        { RoleActeur = "Contrôleur" }, cancellationToken);
    }

    public async Task<DocumentPrevisionDto> GenererValidationUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default)
    {
        var ctx = await BuildUbContextAsync(idVersion, idUB, idUtilisateur, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("VALIDER_UB", idUtilisateur, cancellationToken);
        var ligne = new DocumentPrevisionUbLigneDto(
            idUB, ctx.CodeUB, ctx.LibelleUB, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Dc + ctx.Ae + ctx.Bi,
            StatutVersionBudgetaire.Controlee, StatutVersionBudgetaire.Validee);
        return await CreerAsync(await EnrichUbCommandAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Validation, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            ctx.IdDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            idUB, ctx.CodeUB, ctx.LibelleUB, DocumentPrevisionPortee.Ub, 1,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            StatutVersionBudgetaire.Controlee, StatutVersionBudgetaire.Validee,
            null, null, ctx.Dc, ctx.Ae, ctx.Bi, [ligne]), idVersion, idUB, cancellationToken), cancellationToken);
    }

    public async Task<DocumentPrevisionDto?> GenererValidationDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default)
    {
        if (ubTraitees.Count == 0) return null;
        var ctx = await BuildDepartementContextAsync(idVersion, idDepartement, idUtilisateur, ubTraitees, cancellationToken);
        var audit = idAudit ?? await _repository.FindLatestAuditIdAsync("VALIDATION_DEPARTEMENT", idUtilisateur, cancellationToken);
        return await CreerAsync(new DocumentPrevisionCreateCommand(
            DocumentPrevisionType.Validation, audit, idVersion, ctx.Annee, ctx.NumeroVersion, ctx.LibelleVersion,
            idDepartement, ctx.CodeDepartement, ctx.LibelleDepartement,
            null, null, null, DocumentPrevisionPortee.Departement, ctx.Lignes.Count,
            idUtilisateur, ctx.NomAuteur, DateTime.Now,
            StatutVersionBudgetaire.Controlee, StatutVersionBudgetaire.Validee,
            null, null, ctx.Dc, ctx.Ae, ctx.Bi, ctx.Lignes)
        { RoleActeur = "Validateur" }, cancellationToken);
    }

    public Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idDocument, cancellationToken);

    public Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
        => _repository.GetByReferenceAsync(reference, cancellationToken);

    public Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default)
        => _repository.GetByAuditAsync(idAudit, cancellationToken);

    public async Task<byte[]?> GetPdfBytesAsync(long idDocument, CancellationToken cancellationToken = default)
    {
        var fichier = await _repository.GetFichierAsync(idDocument, cancellationToken);
        if (fichier is null) return null;
        return await _fileStore.ReadAsync(fichier.Value.CheminFichier, cancellationToken);
    }

    private async Task<DocumentPrevisionDto> CreerAsync(
        DocumentPrevisionCreateCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TypeDocument == DocumentPrevisionType.Rejet && string.IsNullOrWhiteSpace(command.Motif))
        {
            throw new InvalidOperationException("Le motif de rejet est obligatoire pour l'avis de rejet.");
        }

        var seq = await _repository.AllouerNumeroAsync(
            command.AnneeExercice, command.IdDepartement, command.NumeroVersion, command.TypeDocument, cancellationToken);
        var reference = DocumentReferenceGenerator.Build(
            command.CodeDepartement, command.AnneeExercice, command.NumeroVersion, command.TypeDocument, seq);

        var total = command.MontantDC + command.MontantAE + command.MontantBI;
        var payload = new DocumentPrevisionPayloadDto(
            DocumentPrevisionType.Titre(command.TypeDocument),
            reference,
            command.TypeDocument,
            command.Portee,
            command.NbUbConcernees,
            command.AnneeExercice,
            command.IdVersion,
            command.NumeroVersion,
            command.LibelleVersion,
            command.IdDepartement,
            command.CodeDepartement,
            command.LibelleDepartement,
            command.IdUB,
            command.CodeUB,
            command.LibelleUB,
            command.DateEvenement,
            command.IdUtilisateurAuteur,
            command.NomUtilisateurAuteur,
            command.StatutAvant,
            command.StatutApres,
            command.Motif,
            command.Observations,
            command.MontantDC,
            command.MontantAE,
            command.MontantBI,
            total,
            command.UbConcernees)
        {
            Devise = "USD",
            CodeMode = command.CodeMode,
            EstMensuel = command.EstMensuel,
            RoleActeur = command.RoleActeur,
            ActionLibelle = DocumentPrevisionType.ActionLibelle(command.TypeDocument),
            StatutAffiche = DocumentPrevisionType.StatutAffiche(command.TypeDocument),
            SignatureLibelle = DocumentPrevisionType.SignatureRole(command.TypeDocument),
            LignesDetail = command.LignesDetail,
        };

        var pdf = _pdfRenderer.Render(payload);
        var hash = Convert.ToHexString(SHA256.HashData(pdf));
        var (relative, _) = await _fileStore.SaveAsync(reference, pdf, cancellationToken);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);

        return await _repository.InsertAsync(
            command,
            reference,
            payloadJson,
            relative,
            hash,
            pdf.LongLength,
            cancellationToken);
    }

    private async Task<DocumentPrevisionCreateCommand> EnrichUbCommandAsync(
        DocumentPrevisionCreateCommand command,
        long idVersion,
        long idUB,
        CancellationToken cancellationToken)
    {
        var mode = await _repository.GetModeDominantAsync(idVersion, idUB, cancellationToken);
        var lignes = await _repository.GetLignesDetailUbAsync(idVersion, idUB, cancellationToken);
        var role = command.TypeDocument switch
        {
            DocumentPrevisionType.Controle => "Contrôleur",
            DocumentPrevisionType.Validation => "Validateur",
            DocumentPrevisionType.Rejet => "Responsable",
            _ => "Utilisateur",
        };
        return command with
        {
            CodeMode = mode.CodeMode,
            EstMensuel = mode.EstMensuel || lignes.Any(l => l.EstMensuel),
            RoleActeur = role,
            LignesDetail = lignes,
        };
    }

    private async Task<(
        short Annee, int NumeroVersion, string? LibelleVersion,
        long IdDepartement, string CodeDepartement, string LibelleDepartement,
        string CodeUB, string LibelleUB, string NomAuteur,
        decimal Dc, decimal Ae, decimal Bi)> BuildUbContextAsync(
        long idVersion, long idUB, long idUtilisateur, CancellationToken cancellationToken)
    {
        var version = await _repository.GetVersionInfoAsync(idVersion, cancellationToken)
            ?? throw new InvalidOperationException("Version budgétaire introuvable.");
        var ub = await _repository.GetUbContexteAsync(idUB, cancellationToken)
            ?? throw new InvalidOperationException("Unité budgétaire introuvable.");
        var nom = await _repository.GetUtilisateurNomAsync(idUtilisateur, cancellationToken)
            ?? $"Utilisateur #{idUtilisateur}";
        var m = await _repository.GetMontantsAsync(idVersion, idUB, cancellationToken);
        return (version.Annee, version.NumeroVersion, version.Libelle,
            ub.IdDepartement, ub.CodeDepartement, ub.LibelleDepartement,
            ub.CodeUB, ub.LibelleUB, nom, m.Dc, m.Ae, m.Bi);
    }

    private async Task<(
        short Annee, int NumeroVersion, string? LibelleVersion,
        string CodeDepartement, string LibelleDepartement, string NomAuteur,
        decimal Dc, decimal Ae, decimal Bi,
        IReadOnlyList<DocumentPrevisionUbLigneDto> Lignes)> BuildDepartementContextAsync(
        long idVersion, long idDepartement, long idUtilisateur, IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken)
    {
        var version = await _repository.GetVersionInfoAsync(idVersion, cancellationToken)
            ?? throw new InvalidOperationException("Version budgétaire introuvable.");
        var dept = await _repository.GetDepartementAsync(idDepartement, cancellationToken)
            ?? throw new InvalidOperationException("Département introuvable.");
        var nom = await _repository.GetUtilisateurNomAsync(idUtilisateur, cancellationToken)
            ?? $"Utilisateur #{idUtilisateur}";

        var ids = ubIds.Distinct().ToList();
        // 2 requêtes batch (plus de N+1) + filtre isolation département
        var infos = await _repository.GetUbInfosBatchAsync(ids, cancellationToken);
        var montants = await _repository.GetMontantsBatchAsync(idVersion, ids, cancellationToken);

        var lignes = new List<DocumentPrevisionUbLigneDto>();
        decimal dc = 0, ae = 0, bi = 0;
        foreach (var idUb in ids)
        {
            if (!infos.TryGetValue(idUb, out var ubInfo)) continue;
            if (ubInfo.IdDepartement != idDepartement) continue;
            montants.TryGetValue(idUb, out var m);
            dc += m.Dc;
            ae += m.Ae;
            bi += m.Bi;
            lignes.Add(new DocumentPrevisionUbLigneDto(
                idUb, ubInfo.CodeUB, ubInfo.LibelleUB, m.Dc, m.Ae, m.Bi, m.Dc + m.Ae + m.Bi));
        }

        return (version.Annee, version.NumeroVersion, version.Libelle,
            dept.Code, dept.Libelle, nom, dc, ae, bi, lignes);
    }
}
