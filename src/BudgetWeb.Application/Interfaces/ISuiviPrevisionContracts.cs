using BudgetWeb.Application.DTOs;

namespace BudgetWeb.Application.Interfaces;

public interface ISuiviPrevisionRepository
{
    Task<SuiviPrevisionListeDto> GetResumeParUbAsync(
        long? idUtilisateurCreation,
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default);

    Task<SuiviUbDetailDto?> GetUbDetailAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);

    Task<SuiviUbDetailLignesDto> GetUbLignesAsync(
        long idVersion,
        long idUB,
        string codeType,
        CancellationToken cancellationToken = default);
}

public interface ISuiviPrevisionService
{
    Task<SuiviPrevisionListeDto> GetMesPrevisionsAsync(
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default);

    Task<SuiviPrevisionListeDto> GetSoumissionsAsync(
        long? idExercice,
        long? idVersion,
        long? idDepartement,
        string? statut,
        string? searchUb,
        CancellationToken cancellationToken = default);

    Task<SuiviUbDetailDto?> GetUbDetailAsync(
        long idVersion,
        long idUB,
        CancellationToken cancellationToken = default);

    Task<SuiviUbDetailLignesDto> GetUbLignesAsync(
        long idVersion,
        long idUB,
        string codeType,
        CancellationToken cancellationToken = default);
}
