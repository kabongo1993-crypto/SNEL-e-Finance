using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class DepartementRepository : IDepartementRepository
{
    private readonly BudgetDbContext _context;

    public DepartementRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DepartementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Departements
            .AsNoTracking()
            .OrderBy(d => d.Code)
            .Select(d => new DepartementDto(
                d.IdDepartement,
                d.Code,
                d.Libelle,
                d.Actif,
                d.DateCreation))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        // Comparaison insensible à la casse (codes normalisés en majuscules côté service).
        return _context.Departements
            .AsNoTracking()
            .AnyAsync(d => d.Code.ToUpper() == code, cancellationToken);
    }

    public async Task<DepartementDto> CreateAsync(
        string code,
        string libelle,
        bool actif,
        CancellationToken cancellationToken = default)
    {
        var entity = new Departement
        {
            Code = code,
            Libelle = libelle,
            Actif = actif,
            DateCreation = DateTime.UtcNow,
        };

        _context.Departements.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new DepartementDto(
            entity.IdDepartement,
            entity.Code,
            entity.Libelle,
            entity.Actif,
            entity.DateCreation);
    }
}
