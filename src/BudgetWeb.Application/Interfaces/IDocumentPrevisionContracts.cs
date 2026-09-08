using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IDocumentPrevisionService
{
    Task<DocumentPrevisionDto> GenererSoumissionUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GenererSoumissionDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto> GenererRejetUbAsync(
        long idVersion, long idUB, long idUtilisateur, string motif, string? statutAvant,
        long? idAudit, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GenererRejetDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, string motif, long? idAudit,
        IReadOnlyList<(long IdUB, string? StatutAvant)> ubTraitees, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto> GenererControleUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GenererControleDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto> GenererValidationUbAsync(
        long idVersion, long idUB, long idUtilisateur, long? idAudit, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GenererValidationDepartementAsync(
        long idVersion, long idDepartement, long idUtilisateur, long? idAudit,
        IReadOnlyList<long> ubTraitees, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default);

    Task<byte[]?> GetPdfBytesAsync(long idDocument, CancellationToken cancellationToken = default);
}

public interface IDocumentPrevisionRepository
{
    Task<int> AllouerNumeroAsync(
        short annee, long idDepartement, int numeroVersion, string typeDocument,
        CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto> InsertAsync(
        DocumentPrevisionCreateCommand command,
        string reference,
        string payloadJson,
        string cheminFichier,
        string hashSha256,
        long tailleOctets,
        CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default);

    Task<(string CheminFichier, string HashSha256)?> GetFichierAsync(
        long idDocument, CancellationToken cancellationToken = default);

    Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
        long idVersion, CancellationToken cancellationToken = default);

    Task<(long IdDepartement, string Code, string Libelle)?> GetDepartementAsync(
        long idDepartement, CancellationToken cancellationToken = default);

    Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement, string CodeUB, string LibelleUB)?> GetUbContexteAsync(
        long idUB, CancellationToken cancellationToken = default);

    Task<string?> GetUtilisateurNomAsync(long idUtilisateur, CancellationToken cancellationToken = default);

    Task<(decimal Dc, decimal Ae, decimal Bi)> GetMontantsAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, (decimal Dc, decimal Ae, decimal Bi)>> GetMontantsBatchAsync(
        long idVersion, IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, (string CodeUB, string LibelleUB, long IdDepartement)>> GetUbInfosBatchAsync(
        IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default);

    Task<(string? CodeMode, bool EstMensuel)> GetModeDominantAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentPrevisionLigneDetailDto>> GetLignesDetailUbAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default);

    Task<long?> FindLatestAuditIdAsync(
        string operation, long idUtilisateur, CancellationToken cancellationToken = default);
}

public interface IDocumentPdfRenderer
{
    byte[] Render(DocumentPrevisionPayloadDto payload);
}

public interface IDocumentFileStore
{
    Task<(string RelativePath, string AbsolutePath)> SaveAsync(
        string reference, byte[] content, CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
}
