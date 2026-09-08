using System.Data;
using System.Data.Common;
using System.Diagnostics;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace BudgetWeb.IntegrationTests;

/// <summary>Diagnostic temporaire DetailQuery — mesures froid/chaud, volumes, STATISTICS IO.</summary>
public class DetailQueryDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public DetailQueryDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task DetailQuery_Diagnostic_FroidChaud_Volumes()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine("Connection string absente — diagnostic ignoré.");
            return;
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var candidates = await ResolveCandidateIdsAsync(conn);
        _output.WriteLine("=== Volumes par DPM ===");
        foreach (var (id, label) in candidates)
        {
            var vol = await GetVolumesAsync(conn, id);
            _output.WriteLine(
                $"DPM #{id} ({label}) — benef={vol.Benef} imput={vol.Imput} snap={vol.Snap} pieces={vol.Pieces} val={vol.Val} statut={vol.Statut}");
        }

        var interceptor = new CommandTimingInterceptor();
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(interceptor)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;

        var runIds = new[]
        {
            candidates.First(c => c.Label is "light" or "heavy_or_50").Id,
            (candidates.FirstOrDefault(c => c.Label == "heavy_or_50") is { } h ? h.Id
                : candidates.First(c => c.Label != "light").Id),
            (candidates.FirstOrDefault(c => c.Label == "multi_imput") is { } m ? m.Id
                : candidates.Last().Id),
        };

        _output.WriteLine("=== Mesures GetDetailAsync (même processus, plan SQL déjà compilé après run 1) ===");
        for (var run = 1; run <= 3; run++)
        {
            var id = runIds[run - 1];
            await using var db = new BudgetDbContext(options);
            db.Database.SetCommandTimeout(120);

            interceptor.Reset();
            var swTotal = Stopwatch.StartNew();
            var entity = await BuildDetailQuery(db).FirstOrDefaultAsync(d => d.IdDemandePaiement == id);
            swTotal.Stop();

            var cmd = interceptor.LastCommandMs;
            var mat = Math.Max(0, swTotal.ElapsedMilliseconds - cmd);

            _output.WriteLine(
                $"Run {run} DPM #{id} — SQL={cmd}ms EF_mat≈{mat}ms Total={swTotal.ElapsedMilliseconds}ms " +
                $"benef={entity?.Beneficiaires.Count ?? 0} imput={entity?.Imputations.Count ?? 0} " +
                $"snap={entity?.Imputations.Sum(i => i.Snapshots.Count) ?? 0} pieces={entity?.PiecesJointes.Count ?? 0}");
        }

        var statsId = candidates.First(c => c.Label == "light").Id;
        _output.WriteLine($"=== STATISTICS IO/TIME simplifié (FK lookups DPM #{statsId}) ===");
        _output.WriteLine(await RunFkLookupStatsAsync(conn, statsId));

        _output.WriteLine("=== Index FK collections (sys.indexes) ===");
        _output.WriteLine(await ListFkIndexesAsync(conn));
    }

    internal static IQueryable<DemandePaiement> BuildDetailQuery(BudgetDbContext ctx)
        => ctx.DemandesPaiement.AsNoTracking()
            .Include(d => d.CasDossier)
            .Include(d => d.ExerciceBudgetaire)
            .Include(d => d.VersionBudgetaire)
            .Include(d => d.UniteBudgetaire).ThenInclude(u => u.Departement)
            .Include(d => d.Demandeur)
            .Include(d => d.TypeBudget)
            .Include(d => d.UtilisateurAssigne)
            .Include(d => d.TauxChange)
            .Include(d => d.TauxChangePaiement)
            .Include(d => d.Beneficiaires.OrderBy(b => b.Ordre))
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.TypeBudget)
            .Include(d => d.Imputations.OrderBy(i => i.Ordre)).ThenInclude(i => i.Snapshots)
            .Include(d => d.PiecesJointes.OrderBy(p => p.DateUpload))
            .Include(d => d.ValidationsEntite.OrderBy(v => v.Ordre))
                .ThenInclude(v => v.UtilisateurValidateur)
            .Include(d => d.ValidationsEntite.OrderBy(v => v.Ordre))
                .ThenInclude(v => v.UtilisateurDeclarant)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurEtabli)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurApprouve)
            .Include(d => d.BilletConversion)
                .ThenInclude(b => b!.UtilisateurVisa)
            .Include(d => d.PieceCaisse)
                .ThenInclude(p => p!.UtilisateurEtabli)
            .Include(d => d.BonProvisoire)
                .ThenInclude(b => b!.UtilisateurEtabli)
            .Include(d => d.MinuteCheque)
                .ThenInclude(m => m!.UtilisateurEtabli)
            .AsSplitQuery();

    private static async Task<List<(long Id, string Label)>> ResolveCandidateIdsAsync(SqlConnection conn)
    {
        async Task<long?> Scalar(string sql)
        {
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            var o = await cmd.ExecuteScalarAsync();
            return o is null or DBNull ? null : Convert.ToInt64(o);
        }

        var light = await Scalar("""
            SELECT TOP 1 d.IdDemandePaiement FROM dpm.DEMANDE_PAIEMENT d
            WHERE d.Statut = 'BROUILLON' ORDER BY d.IdDemandePaiement DESC
            """);
        var heavy50 = await Scalar("SELECT IdDemandePaiement FROM dpm.DEMANDE_PAIEMENT WHERE IdDemandePaiement = 50");
        var heavy = heavy50 ?? await Scalar("""
            SELECT TOP 1 d.IdDemandePaiement FROM dpm.DEMANDE_PAIEMENT d
            ORDER BY (
                (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE b WHERE b.FK_DemandePaiement = d.IdDemandePaiement)
              + (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = d.IdDemandePaiement)
              + (SELECT COUNT(*) FROM dpm.PIECE_JOINTE p WHERE p.FK_DemandePaiement = d.IdDemandePaiement)
            ) DESC
            """);
        var multiImput = await Scalar("""
            SELECT TOP 1 d.IdDemandePaiement FROM dpm.DEMANDE_PAIEMENT d
            WHERE (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = d.IdDemandePaiement) >= 2
            ORDER BY (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = d.IdDemandePaiement) DESC
            """);

        var list = new List<(long, string)>();
        if (light is long l) list.Add((l, "light"));
        if (heavy is long h) list.Add((h, heavy50 is not null ? "heavy_or_50" : "heavy"));
        if (multiImput is long m && list.All(x => x.Item1 != m)) list.Add((m, "multi_imput"));
        if (list.Count == 0) list.Add((1L, "fallback"));
        return list;
    }

    private static async Task<(int Benef, int Imput, int Snap, int Pieces, int Val, string Statut)> GetVolumesAsync(
        SqlConnection conn, long id)
    {
        const string sql = """
            SELECT d.Statut,
                   (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE b WHERE b.FK_DemandePaiement = d.IdDemandePaiement),
                   (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = d.IdDemandePaiement),
                   (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT s WHERE s.FK_DemandePaiement = d.IdDemandePaiement),
                   (SELECT COUNT(*) FROM dpm.PIECE_JOINTE p WHERE p.FK_DemandePaiement = d.IdDemandePaiement),
                   (SELECT COUNT(*) FROM dpm.DEMANDE_PAIEMENT_VALIDATION v WHERE v.FK_DemandePaiement = d.IdDemandePaiement)
            FROM dpm.DEMANDE_PAIEMENT d WHERE d.IdDemandePaiement = @id
            """;
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return (0, 0, 0, 0, 0, "?");
        return (r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetString(0));
    }

    private static async Task<string> RunFkLookupStatsAsync(SqlConnection conn, long id)
    {
        var sb = new System.Text.StringBuilder();
        var batches = new[]
        {
            "DBCC FREEPROCCACHE WITH NO_INFOMSGS; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;",
            """
            SET STATISTICS IO ON; SET STATISTICS TIME ON;
            SELECT b.* FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE b WHERE b.FK_DemandePaiement = @id;
            SELECT i.* FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = @id;
            SELECT p.* FROM dpm.PIECE_JOINTE p WHERE p.FK_DemandePaiement = @id;
            SELECT v.* FROM dpm.DEMANDE_PAIEMENT_VALIDATION v WHERE v.FK_DemandePaiement = @id;
            SET STATISTICS IO OFF; SET STATISTICS TIME OFF;
            """,
            """
            SET STATISTICS IO ON; SET STATISTICS TIME ON;
            SELECT b.* FROM dpm.DEMANDE_PAIEMENT_BENEFICIAIRE b WHERE b.FK_DemandePaiement = @id;
            SELECT i.* FROM dpm.DEMANDE_PAIEMENT_IMPUTATION i WHERE i.FK_DemandePaiement = @id;
            SELECT p.* FROM dpm.PIECE_JOINTE p WHERE p.FK_DemandePaiement = @id;
            SELECT v.* FROM dpm.DEMANDE_PAIEMENT_VALIDATION v WHERE v.FK_DemandePaiement = @id;
            SET STATISTICS IO OFF; SET STATISTICS TIME OFF;
            """,
        };

        foreach (var batch in batches)
        {
            await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
            if (batch.Contains("@id"))
                cmd.Parameters.AddWithValue("@id", id);
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync();
                do
                {
                    while (await reader.ReadAsync())
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                            sb.Append(reader.GetValue(i)).Append('\t');
                        sb.AppendLine();
                    }
                } while (await reader.NextResultAsync());
            }
            catch (Exception ex) when (batch.StartsWith("DBCC"))
            {
                sb.AppendLine($"[DBCC skipped: {ex.Message}]");
            }
        }

        return sb.ToString();
    }

    private static async Task<string> ListFkIndexesAsync(SqlConnection conn)
    {
        const string sql = """
            SELECT OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) AS [Table],
                   i.name AS IndexName,
                   i.type_desc,
                   STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) IN (
                'DEMANDE_PAIEMENT','DEMANDE_PAIEMENT_BENEFICIAIRE','DEMANDE_PAIEMENT_IMPUTATION',
                'DEMANDE_PAIEMENT_IMPUTATION_SNAPSHOT','PIECE_JOINTE','DEMANDE_PAIEMENT_VALIDATION',
                'BILLET_CONVERSION','PIECE_CAISSE','BON_PROVISOIRE','MINUTE_CHEQUE')
              AND ic.is_included_column = 0
            GROUP BY i.object_id, i.name, i.type_desc, i.index_id
            ORDER BY 1, 2
            """;
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var sb = new System.Text.StringBuilder();
        while (await r.ReadAsync())
            sb.AppendLine($"{r.GetString(0)} | {r.GetString(1)} | {r.GetString(2)} | {r.GetString(3)}");
        return sb.ToString();
    }

    private sealed class CommandTimingInterceptor : DbCommandInterceptor
    {
        private readonly Stopwatch _sw = new();
        public long LastCommandMs { get; private set; }

        public void Reset() => LastCommandMs = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _sw.Restart();
            return base.ReaderExecuting(command, eventData, result);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            _sw.Stop();
            LastCommandMs += _sw.ElapsedMilliseconds;
            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            _sw.Stop();
            LastCommandMs += _sw.ElapsedMilliseconds;
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _sw.Restart();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
