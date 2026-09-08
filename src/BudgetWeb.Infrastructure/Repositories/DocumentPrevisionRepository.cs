using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetWeb.Infrastructure.Repositories;

public class DocumentPrevisionRepository : IDocumentPrevisionRepository
{
    private readonly BudgetDbContext _context;

    public DocumentPrevisionRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task<int> AllouerNumeroAsync(
        short annee,
        long idDepartement,
        int numeroVersion,
        string typeDocument,
        CancellationToken cancellationToken = default)
    {
        var type = typeDocument.Trim().ToUpperInvariant();
        // Allocation atomique SQL Server (UPDLOCK + HOLDLOCK) pour éviter les collisions concurrentes.
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
IF NOT EXISTS (
    SELECT 1 FROM DOCUMENT_PREVISION_SEQUENCE WITH (UPDLOCK, HOLDLOCK)
    WHERE Annee = {annee} AND IdDepartement = {idDepartement}
      AND NumeroVersion = {numeroVersion} AND TypeDocument = {type})
BEGIN
    INSERT INTO DOCUMENT_PREVISION_SEQUENCE (Annee, IdDepartement, NumeroVersion, TypeDocument, DernierNumero)
    VALUES ({annee}, {idDepartement}, {numeroVersion}, {type}, 0);
END
UPDATE DOCUMENT_PREVISION_SEQUENCE WITH (UPDLOCK, ROWLOCK)
SET DernierNumero = DernierNumero + 1
WHERE Annee = {annee} AND IdDepartement = {idDepartement}
  AND NumeroVersion = {numeroVersion} AND TypeDocument = {type};
", cancellationToken);

            var numero = await _context.DocumentPrevisionSequences.AsNoTracking()
                .Where(s => s.Annee == annee
                            && s.IdDepartement == idDepartement
                            && s.NumeroVersion == numeroVersion
                            && s.TypeDocument == type)
                .Select(s => s.DernierNumero)
                .SingleAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return numero;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DocumentPrevisionDto> InsertAsync(
        DocumentPrevisionCreateCommand command,
        string reference,
        string payloadJson,
        string cheminFichier,
        string hashSha256,
        long tailleOctets,
        CancellationToken cancellationToken = default)
    {
        var total = command.MontantDC + command.MontantAE + command.MontantBI;
        var entity = new DocumentPrevision
        {
            Reference = reference,
            TypeDocument = command.TypeDocument,
            IdAudit = command.IdAudit,
            IdVersion = command.IdVersion,
            AnneeExercice = command.AnneeExercice,
            NumeroVersion = command.NumeroVersion,
            IdDepartement = command.IdDepartement,
            CodeDepartement = command.CodeDepartement,
            LibelleDepartement = command.LibelleDepartement,
            IdUB = command.IdUB,
            CodeUB = command.CodeUB,
            LibelleUB = command.LibelleUB,
            Portee = command.Portee,
            NbUbConcernees = command.NbUbConcernees,
            IdUtilisateurAuteur = command.IdUtilisateurAuteur,
            NomUtilisateurAuteur = command.NomUtilisateurAuteur,
            DateEvenement = command.DateEvenement,
            StatutAvant = command.StatutAvant,
            StatutApres = command.StatutApres,
            Motif = command.Motif,
            MontantDC = command.MontantDC,
            MontantAE = command.MontantAE,
            MontantBI = command.MontantBI,
            MontantTotal = total,
            PayloadJson = payloadJson,
            CheminFichier = cheminFichier,
            HashSha256 = hashSha256,
            TailleOctets = tailleOctets,
            DateGeneration = DateTime.Now,
        };

        _context.DocumentsPrevision.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default)
    {
        var e = await _context.DocumentsPrevision.AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDocument == idDocument, cancellationToken);
        return e is null ? null : Map(e);
    }

    public async Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var refNorm = (reference ?? string.Empty).Trim();
        var e = await _context.DocumentsPrevision.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Reference == refNorm, cancellationToken);
        return e is null ? null : Map(e);
    }

    public async Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default)
    {
        var e = await _context.DocumentsPrevision.AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdAudit == idAudit, cancellationToken);
        return e is null ? null : Map(e);
    }

    public async Task<(string CheminFichier, string HashSha256)?> GetFichierAsync(
        long idDocument, CancellationToken cancellationToken = default)
    {
        var row = await _context.DocumentsPrevision.AsNoTracking()
            .Where(d => d.IdDocument == idDocument)
            .Select(d => new { d.CheminFichier, d.HashSha256 })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.CheminFichier, row.HashSha256);
    }

    public async Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
        long idVersion, CancellationToken cancellationToken = default)
    {
        var row = await _context.VersionsBudgetaires.AsNoTracking()
            .Where(v => v.IdVersion == idVersion)
            .Select(v => new
            {
                Annee = v.ExerciceBudgetaire.Annee,
                v.NumeroVersion,
                v.Libelle,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Annee, row.NumeroVersion, row.Libelle);
    }

    public async Task<(long IdDepartement, string Code, string Libelle)?> GetDepartementAsync(
        long idDepartement, CancellationToken cancellationToken = default)
    {
        var row = await _context.Departements.AsNoTracking()
            .Where(d => d.IdDepartement == idDepartement)
            .Select(d => new { d.IdDepartement, d.Code, d.Libelle })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.IdDepartement, row.Code, row.Libelle);
    }

    public async Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement, string CodeUB, string LibelleUB)?> GetUbContexteAsync(
        long idUB, CancellationToken cancellationToken = default)
    {
        var row = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => u.IdUB == idUB)
            .Select(u => new
            {
                u.FK_Departement,
                CodeDepartement = u.Departement.Code,
                LibelleDepartement = u.Departement.Libelle,
                u.CodeUB,
                u.Libelle,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : (row.FK_Departement, row.CodeDepartement, row.LibelleDepartement, row.CodeUB, row.Libelle);
    }

    public async Task<string?> GetUtilisateurNomAsync(long idUtilisateur, CancellationToken cancellationToken = default)
    {
        var row = await _context.Utilisateurs.AsNoTracking()
            .Where(u => u.IdUtilisateur == idUtilisateur)
            .Select(u => new { u.Prenom, u.Nom, u.NomUtilisateur })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var display = $"{row.Prenom} {row.Nom}".Trim();
        return string.IsNullOrWhiteSpace(display) ? row.NomUtilisateur : display;
    }

    public async Task<(decimal Dc, decimal Ae, decimal Bi)> GetMontantsAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var map = await GetMontantsBatchAsync(idVersion, [idUB], cancellationToken);
        return map.TryGetValue(idUB, out var m) ? m : (0, 0, 0);
    }

    public async Task<IReadOnlyDictionary<long, (decimal Dc, decimal Ae, decimal Bi)>> GetMontantsBatchAsync(
        long idVersion,
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ubIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, (decimal, decimal, decimal)>();
        }

        var rows = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion && ids.Contains(p.FK_UniteBudgetaire))
            .GroupBy(p => new { p.FK_UniteBudgetaire, Code = p.TypeBudget.CodeType })
            .Select(g => new { g.Key.FK_UniteBudgetaire, g.Key.Code, Montant = g.Sum(x => x.MontantAnnuel) })
            .ToListAsync(cancellationToken);

        var result = ids.ToDictionary(id => id, _ => (Dc: 0m, Ae: 0m, Bi: 0m));
        foreach (var r in rows)
        {
            var cur = result[r.FK_UniteBudgetaire];
            var c = (r.Code ?? "").Trim().ToUpperInvariant();
            if (c == "DC") cur.Dc = r.Montant;
            else if (c == "AE") cur.Ae = r.Montant;
            else if (c == "BI") cur.Bi = r.Montant;
            result[r.FK_UniteBudgetaire] = cur;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<long, (string CodeUB, string LibelleUB, long IdDepartement)>> GetUbInfosBatchAsync(
        IReadOnlyList<long> ubIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ubIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, (string, string, long)>();
        }

        var rows = await _context.UnitesBudgetaires.AsNoTracking()
            .Where(u => ids.Contains(u.IdUB))
            .Select(u => new { u.IdUB, u.CodeUB, u.Libelle, u.FK_Departement })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.IdUB, r => (r.CodeUB, r.Libelle, r.FK_Departement));
    }

    public async Task<(string? CodeMode, bool EstMensuel)> GetModeDominantAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var row = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB)
            .GroupBy(p => p.ModePrevision.CodeMode)
            .Select(g => new { Code = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return (null, false);
        var code = (row.Code ?? "").Trim().ToUpperInvariant();
        return (row.Code, code is "MENSUEL" or "MENS");
    }

    public async Task<IReadOnlyList<DocumentPrevisionLigneDetailDto>> GetLignesDetailUbAsync(
        long idVersion, long idUB, CancellationToken cancellationToken = default)
    {
        var previsions = await _context.PrevisionsBudgetaires.AsNoTracking()
            .Where(p => p.FK_VersionBudgetaire == idVersion && p.FK_UniteBudgetaire == idUB)
            .Select(p => new
            {
                CodeType = p.TypeBudget.CodeType,
                CodeMode = p.ModePrevision.CodeMode,
                p.MontantAnnuel,
                CodeRB = p.RubriqueBudgetaire != null ? p.RubriqueBudgetaire.CodeRB : null,
                LibelleRB = p.RubriqueBudgetaire != null ? p.RubriqueBudgetaire.Libelle : null,
                CodeGroupe = p.RubriqueBudgetaire != null && p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.CodeGroupe
                    : null,
                LibelleGroupe = p.RubriqueBudgetaire != null && p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.Libelle
                    : null,
                OrdreGroupe = p.RubriqueBudgetaire != null && p.RubriqueBudgetaire.GroupeRubriqueBudgetaire != null
                    ? (int?)p.RubriqueBudgetaire.GroupeRubriqueBudgetaire.OrdreAffichage
                    : null,
                p.LibelleItemAE,
                p.DetailBI,
                CodeItemBI = p.ItemBI != null ? p.ItemBI.CodeItem : null,
                LibelleItemBI = p.ItemBI != null ? p.ItemBI.Libelle : null,
                Mois = p.RepartitionsMensuelles.Select(r => new { r.Mois, r.Montant }),
            })
            .ToListAsync(cancellationToken);

        var result = new List<DocumentPrevisionLigneDetailDto>();
        var dcGroups = previsions
            .Where(p => (p.CodeType ?? "").ToUpperInvariant() == "DC" && p.CodeRB != null)
            .GroupBy(p => new { p.CodeGroupe, p.LibelleGroupe, p.OrdreGroupe })
            .OrderBy(g => g.Key.OrdreGroupe ?? 999)
            .ThenBy(g => g.Key.CodeGroupe);

        foreach (var g in dcGroups)
        {
            var codeG = g.Key.CodeGroupe ?? "—";
            var libG = g.Key.LibelleGroupe ?? "Sans groupe";
            result.Add(new DocumentPrevisionLigneDetailDto(true, codeG, libG, "DC", 0, false));
            foreach (var p in g.OrderBy(x => x.CodeRB))
            {
                var mensuel = (p.CodeMode ?? "").ToUpperInvariant() is "MENSUEL" or "MENS";
                var mois = new decimal[13];
                foreach (var m in p.Mois)
                {
                    if (m.Mois is >= 1 and <= 12) mois[m.Mois] = m.Montant;
                }

                result.Add(new DocumentPrevisionLigneDetailDto(
                    false, p.CodeRB!, p.LibelleRB ?? "", "DC", p.MontantAnnuel, mensuel,
                    mois[1], mois[2], mois[3], mois[4], mois[5], mois[6],
                    mois[7], mois[8], mois[9], mois[10], mois[11], mois[12]));
            }
        }

        foreach (var p in previsions.Where(x => (x.CodeType ?? "").ToUpperInvariant() == "AE"))
        {
            result.Add(new DocumentPrevisionLigneDetailDto(
                false, "AE", p.LibelleItemAE ?? "Action d'exploitation", "AE", p.MontantAnnuel, false));
        }

        foreach (var p in previsions.Where(x => (x.CodeType ?? "").ToUpperInvariant() == "BI"))
        {
            var code = p.CodeItemBI ?? "BI";
            var lib = string.IsNullOrWhiteSpace(p.DetailBI) ? (p.LibelleItemBI ?? "Budget d'investissement") : p.DetailBI!;
            result.Add(new DocumentPrevisionLigneDetailDto(false, code, lib, "BI", p.MontantAnnuel, false));
        }

        return result;
    }

    public async Task<long?> FindLatestAuditIdAsync(
        string operation, long idUtilisateur, CancellationToken cancellationToken = default)
    {
        return await _context.JournalAudits.AsNoTracking()
            .Where(j => j.Entite == "WORKFLOW_PREVISION_UB"
                        && j.Operation == operation
                        && j.FK_Utilisateur == idUtilisateur)
            .OrderByDescending(j => j.IdAudit)
            .Select(j => (long?)j.IdAudit)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static DocumentPrevisionDto Map(DocumentPrevision e)
        => new(
            e.IdDocument,
            e.Reference,
            e.TypeDocument,
            DocumentPrevisionType.Titre(e.TypeDocument),
            e.IdAudit,
            e.IdVersion,
            e.AnneeExercice,
            e.NumeroVersion,
            e.IdDepartement,
            e.CodeDepartement,
            e.LibelleDepartement,
            e.IdUB,
            e.CodeUB,
            e.LibelleUB,
            e.Portee,
            e.NbUbConcernees,
            e.IdUtilisateurAuteur,
            e.NomUtilisateurAuteur,
            e.DateEvenement,
            e.StatutAvant,
            e.StatutApres,
            e.Motif,
            e.MontantDC,
            e.MontantAE,
            e.MontantBI,
            e.MontantTotal,
            e.DateGeneration,
            e.TailleOctets);
}
