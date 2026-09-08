using BudgetWeb.Application.DTOs.Rapports;

namespace BudgetWeb.Application.Interfaces;

public interface IRapportPrevisionDcConsolideService
{
    Task<RapportDcConsolideDto> GetAsync(RapportDcConsolideQuery query, CancellationToken cancellationToken = default);

    Task<byte[]> GetPdfAsync(RapportDcConsolideQuery query, CancellationToken cancellationToken = default);
}

public interface IRapportPrevisionDcConsolidePdfRenderer
{
    byte[] Render(RapportDcConsolideDto rapport);

    IReadOnlyList<string> RenderPreviewImages(RapportDcConsolideDto rapport, string outputDirectory);
}
