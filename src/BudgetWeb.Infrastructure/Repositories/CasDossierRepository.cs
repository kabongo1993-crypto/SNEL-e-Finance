using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class CasDossierRepository : ICasDossierRepository
{
    private readonly BudgetDbContext _context;

    public CasDossierRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CasDossier>> ListAsync(bool? actifsSeulement, CancellationToken cancellationToken = default)
    {
        var q = _context.CasDossiers.AsNoTracking().AsQueryable();
        if (actifsSeulement == true)
            q = q.Where(c => c.Actif);
        return await q
            .OrderBy(c => c.Ordre)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<CasDossier?> GetByIdAsync(long idCasDossier, CancellationToken cancellationToken = default)
        => _context.CasDossiers.FirstOrDefaultAsync(c => c.IdCasDossier == idCasDossier, cancellationToken);

    public Task<CasDossier?> GetWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default)
        => _context.CasDossiers.AsNoTracking()
            .Include(c => c.PiecesObligatoires.OrderBy(p => p.Ordre))
            .FirstOrDefaultAsync(c => c.IdCasDossier == idCasDossier, cancellationToken);

    public Task<CasDossier?> GetTrackedWithPiecesAsync(long idCasDossier, CancellationToken cancellationToken = default)
        => _context.CasDossiers
            .Include(c => c.PiecesObligatoires.OrderBy(p => p.Ordre))
            .FirstOrDefaultAsync(c => c.IdCasDossier == idCasDossier, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, long? excludeId, CancellationToken cancellationToken = default)
        => _context.CasDossiers.AsNoTracking()
            .AnyAsync(
                c => c.Code == code && (excludeId == null || c.IdCasDossier != excludeId),
                cancellationToken);

    public async Task<CasDossier> AddAsync(CasDossier entity, CancellationToken cancellationToken = default)
    {
        _context.CasDossiers.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<CasDossierPieceObligatoire?> GetPieceByIdAsync(long idPieceObligatoire, CancellationToken cancellationToken = default)
        => _context.CasDossierPiecesObligatoires.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPieceObligatoire == idPieceObligatoire, cancellationToken);

    public Task<CasDossierPieceObligatoire?> GetPieceTrackedAsync(long idPieceObligatoire, CancellationToken cancellationToken = default)
        => _context.CasDossierPiecesObligatoires
            .FirstOrDefaultAsync(p => p.IdPieceObligatoire == idPieceObligatoire, cancellationToken);

    public Task<bool> PieceCodeExistsAsync(
        long idCasDossier,
        string codeTypePiece,
        long? excludeId,
        CancellationToken cancellationToken = default)
        => _context.CasDossierPiecesObligatoires.AsNoTracking()
            .AnyAsync(
                p => p.FK_CasDossier == idCasDossier
                     && p.CodeTypePiece == codeTypePiece
                     && (excludeId == null || p.IdPieceObligatoire != excludeId),
                cancellationToken);

    public Task<bool> PieceEstReferenceeAsync(long idPieceObligatoire, CancellationToken cancellationToken = default)
        => _context.PiecesJointes.AsNoTracking()
            .AnyAsync(p => p.FK_PieceObligatoire == idPieceObligatoire, cancellationToken);

    public async Task AddPieceAsync(CasDossierPieceObligatoire piece, CancellationToken cancellationToken = default)
    {
        _context.CasDossierPiecesObligatoires.Add(piece);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
