namespace BudgetWeb.Application.DTOs;

public record ExerciceDto(
    long IdExercice,
    short Annee,
    string Statut,
    DateOnly? DateOuverture,
    DateOnly? DateCloture,
    int NombreVersions);

public record CreateExerciceRequest(
    short Annee,
    string Statut,
    DateOnly? DateOuverture,
    DateOnly? DateCloture);

public record UpdateExerciceRequest(
    short Annee,
    string Statut,
    DateOnly? DateOuverture,
    DateOnly? DateCloture);
