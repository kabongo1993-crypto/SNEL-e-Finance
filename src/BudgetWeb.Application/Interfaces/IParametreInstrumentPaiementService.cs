using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IParametreInstrumentPaiementService
{
    Task<IReadOnlyList<ParametreInstrumentPaiementDto>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ParametreInstrumentPaiementDto?> GetByTypeAsync(
        string typeInstrument,
        CancellationToken cancellationToken = default);

    Task<ParametreInstrumentPaiementDto> UpsertAsync(
        string typeInstrument,
        UpsertParametreInstrumentRequest request,
        CancellationToken cancellationToken = default);
}
