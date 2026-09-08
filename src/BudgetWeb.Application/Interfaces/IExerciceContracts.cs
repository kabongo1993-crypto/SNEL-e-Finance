using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface IExerciceRepository
{
    Task<IReadOnlyList<ExerciceDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ExerciceDto?> GetByIdAsync(long idExercice, CancellationToken cancellationToken = default);

    Task<bool> ExistsByAnneeAsync(short annee, long? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountVersionsAsync(long idExercice, CancellationToken cancellationToken = default);

    Task<ExerciceDto> CreateAsync(
        short annee,
        string statut,
        DateOnly? dateOuverture,
        DateOnly? dateCloture,
        CancellationToken cancellationToken = default);

    Task<ExerciceDto?> UpdateAsync(
        long idExercice,
        short annee,
        string statut,
        DateOnly? dateOuverture,
        DateOnly? dateCloture,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idExercice, CancellationToken cancellationToken = default);
}

public interface IExerciceService
{
    Task<IReadOnlyList<ExerciceDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ExerciceDto?> GetByIdAsync(long idExercice, CancellationToken cancellationToken = default);

    Task<ExerciceDto> CreateAsync(
        CreateExerciceRequest request,
        CancellationToken cancellationToken = default);

    Task<ExerciceDto?> UpdateAsync(
        long idExercice,
        UpdateExerciceRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long idExercice, CancellationToken cancellationToken = default);
}
