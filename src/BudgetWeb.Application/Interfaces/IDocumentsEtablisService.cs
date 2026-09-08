using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IDocumentsEtablisService
{
    Task<IReadOnlyList<DocumentEtabliListItemDto>> ListAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenererListePdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenererArchivePdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenererDocumentsPdfAsync(
        DocumentsEtablisQuery query,
        CancellationToken cancellationToken = default);
}

public interface IDocumentsEtablisPdfMerger
{
    byte[] Merge(IReadOnlyList<byte[]> documents);
}

public interface IDocumentsEtablisListePdfRenderer
{
    byte[] Render(DocumentsEtablisQuery query, IReadOnlyList<DocumentEtabliListItemDto> rows);
}
