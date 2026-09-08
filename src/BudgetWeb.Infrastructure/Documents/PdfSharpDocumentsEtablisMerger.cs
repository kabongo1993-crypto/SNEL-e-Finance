using BudgetWeb.Application.Interfaces;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class PdfSharpDocumentsEtablisMerger : IDocumentsEtablisPdfMerger
{
    public byte[] Merge(IReadOnlyList<byte[]> documents)
    {
        if (documents.Count == 0)
            throw new InvalidOperationException("Aucun PDF à fusionner.");
        if (documents.Count == 1)
            return documents[0];

        using var output = new PdfDocument();
        foreach (var bytes in documents)
        {
            using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            for (var i = 0; i < input.PageCount; i++)
                output.AddPage(input.Pages[i]);
        }

        using var buffer = new MemoryStream();
        output.Save(buffer, false);
        return buffer.ToArray();
    }
}
