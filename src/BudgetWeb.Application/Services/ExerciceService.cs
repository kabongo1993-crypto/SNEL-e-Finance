using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Enums;

namespace BudgetWeb.Application.Services;

public class ExerciceService : IExerciceService
{
    private readonly IExerciceRepository _repository;

    public ExerciceService(IExerciceRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ExerciceDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<ExerciceDto?> GetByIdAsync(long idExercice, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idExercice, cancellationToken);

    public async Task<ExerciceDto> CreateAsync(
        CreateExerciceRequest request,
        CancellationToken cancellationToken = default)
    {
        var (annee, statut, dateOuverture, dateCloture) = NormaliserEtValider(request.Annee, request.Statut, request.DateOuverture, request.DateCloture);

        if (await _repository.ExistsByAnneeAsync(annee, excludeId: null, cancellationToken))
        {
            throw new InvalidOperationException($"L'exercice {annee} existe déjà.");
        }

        return await _repository.CreateAsync(annee, statut, dateOuverture, dateCloture, cancellationToken);
    }

    public async Task<ExerciceDto?> UpdateAsync(
        long idExercice,
        UpdateExerciceRequest request,
        CancellationToken cancellationToken = default)
    {
        var (annee, statut, dateOuverture, dateCloture) = NormaliserEtValider(request.Annee, request.Statut, request.DateOuverture, request.DateCloture);

        if (await _repository.ExistsByAnneeAsync(annee, excludeId: idExercice, cancellationToken))
        {
            throw new InvalidOperationException($"L'exercice {annee} existe déjà.");
        }

        if (statut == StatutExerciceBudgetaire.Cloture && dateCloture is null)
        {
            dateCloture = DateOnly.FromDateTime(DateTime.Today);
        }

        if (dateOuverture is { } ouvertureFinale
            && dateCloture is { } clotureFinale
            && clotureFinale < ouvertureFinale)
        {
            throw new InvalidOperationException(
                "La date de clôture ne peut pas être antérieure à la date d'ouverture.");
        }

        return await _repository.UpdateAsync(idExercice, annee, statut, dateOuverture, dateCloture, cancellationToken);
    }

    public async Task<bool> DeleteAsync(long idExercice, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(idExercice, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (await _repository.CountVersionsAsync(idExercice, cancellationToken) > 0)
        {
            throw new InvalidOperationException(
                "Cet exercice ne peut pas être supprimé car il est utilisé par une ou plusieurs versions budgétaires.");
        }

        return await _repository.DeleteAsync(idExercice, cancellationToken);
    }

    private static (short Annee, string Statut, DateOnly? DateOuverture, DateOnly? DateCloture) NormaliserEtValider(
        short annee,
        string? statutBrut,
        DateOnly? dateOuverture,
        DateOnly? dateCloture)
    {
        if (annee < 2000 || annee > 2100)
        {
            throw new InvalidOperationException(
                annee == 0
                    ? "L'année de l'exercice est obligatoire."
                    : "L'année de l'exercice doit être comprise entre 2000 et 2100.");
        }

        var statut = StatutExerciceBudgetaire.Normaliser(statutBrut);
        if (!StatutExerciceBudgetaire.IsValid(statut))
        {
            throw new InvalidOperationException("Le statut de l'exercice doit être OUVERT ou CLOTURE.");
        }

        if (dateOuverture is { } ouverture && dateCloture is { } cloture && cloture < ouverture)
        {
            throw new InvalidOperationException(
                "La date de clôture ne peut pas être antérieure à la date d'ouverture.");
        }

        return (annee, statut, dateOuverture, dateCloture);
    }
}
