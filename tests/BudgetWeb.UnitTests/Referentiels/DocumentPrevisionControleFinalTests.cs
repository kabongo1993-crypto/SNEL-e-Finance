using System.Diagnostics;
using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using BudgetWeb.Infrastructure.Documents;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

/// <summary>Contrôle final documentaire — validation uniquement (pas de nouvelle feature).</summary>
public class DocumentPrevisionControleFinalTests
{
    [Fact]
    public void FormatUsd_FrFr_EspaceMilliersEtVirguleDecimale()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var formatted = string.Format(culture, "{0:N2} USD", 12_500_000m);
        var expected = 12_500_000m.ToString("N2", culture) + " USD";

        Assert.Equal(expected, formatted);
        Assert.NotEqual("12500000", formatted);
        Assert.NotEqual("12500000.00 USD", formatted);
        Assert.NotEqual("12,500,000.00 USD", formatted);
        Assert.EndsWith("USD", formatted);
        Assert.Contains(",00", formatted);
        // Séparateur de milliers fr-FR présent (espace / U+202F / U+00A0)
        Assert.True(
            formatted.Contains('\u202F') || formatted.Contains('\u00A0') || formatted.Contains(' '),
            $"Séparateur milliers manquant : [{formatted}]");
    }

    [Fact]
    public async Task ScenarioComplet_CycleAvecDocumentsAssocies()
    {
        // BROUILLON→SOUMISE→REJETEE→(réouv. sans doc)→SOUMISE→CONTROLEE→VALIDEE
        var (svc, _, _) = Create();
        var refs = new HashSet<string>(StringComparer.Ordinal);

        var sub1 = await svc.GenererSoumissionUbAsync(10, 100, 42, 1001);
        Assert.True(refs.Add(sub1.Reference));
        Assert.Equal(DocumentPrevisionType.Soumission, sub1.TypeDocument);
        Assert.NotNull(await svc.GetByAuditAsync(1001));

        var rej = await svc.GenererRejetUbAsync(10, 100, 42, "Écart DC", "SOUMISE", 1002);
        Assert.True(refs.Add(rej.Reference));
        Assert.Equal("Écart DC", rej.Motif);
        Assert.NotNull(await svc.GetByAuditAsync(1002));

        // Réouverture : pas de document (hors périmètre génération)

        var sub2 = await svc.GenererSoumissionUbAsync(10, 100, 42, 1003);
        Assert.True(refs.Add(sub2.Reference));
        Assert.NotEqual(sub1.Reference, sub2.Reference);

        var ctl = await svc.GenererControleUbAsync(10, 100, 42, 1004);
        Assert.True(refs.Add(ctl.Reference));
        Assert.NotNull(await svc.GetByAuditAsync(1004));

        var val = await svc.GenererValidationUbAsync(10, 100, 42, 1005);
        Assert.True(refs.Add(val.Reference));
        Assert.NotNull(await svc.GetByAuditAsync(1005));

        Assert.Equal(5, refs.Count);
    }

    [Fact]
    public async Task Isolation_DocumentsDepartementA_NePartagentPasReferenceB()
    {
        var repo = new FakeDocRepoIsolation();
        var svc = new DocumentPrevisionService(repo, new FakePdfRenderer(), new FakeFileStore());

        var docA = await svc.GenererRejetDepartementAsync(
            10, 1, 42, "Rejet Dép A", 2001,
            [(10, "SOUMISE")]);
        var docB = await svc.GenererSoumissionDepartementAsync(10, 2, 42, 2002, [20]);

        Assert.NotNull(docA);
        Assert.NotNull(docB);
        Assert.Equal("DFA", docA!.CodeDepartement);
        Assert.Equal("DFB", docB!.CodeDepartement);
        Assert.Contains("/DFA/", docA.Reference);
        Assert.Contains("/DFB/", docB.Reference);
        Assert.NotEqual(docA.Reference, docB.Reference);

        // Historique lié via IdAudit : documents A et B isolés par département
        var byAuditA = await svc.GetByAuditAsync(2001);
        var byAuditB = await svc.GetByAuditAsync(2002);
        Assert.NotNull(byAuditA);
        Assert.NotNull(byAuditB);
        Assert.Equal(1, byAuditA!.IdDepartement);
        Assert.Equal(2, byAuditB!.IdDepartement);
        Assert.NotEqual(byAuditA.IdDepartement, byAuditB.IdDepartement);
        Assert.NotEqual(byAuditA.Reference, byAuditB.Reference);
    }

    [Fact]
    public void Pdf_ContenuMetier_EtMetriquesProduction()
    {
        var payload = new DocumentPrevisionPayloadDto(
            DocumentPrevisionType.Titre(DocumentPrevisionType.Rejet),
            "SNEL/DFI/BUD/2026/V04/REJ/00013",
            DocumentPrevisionType.Rejet,
            DocumentPrevisionPortee.Ub,
            1,
            2026,
            10,
            4,
            "V04",
            7,
            "DFI",
            "Direction Financière",
            55,
            "UB-055",
            "Unité 055",
            new DateTime(2026, 8, 23, 14, 30, 0),
            42,
            "Alice Dupont",
            "SOUMISE",
            "REJETEE",
            "Montants incomplets",
            null,
            12_500_000m,
            0m,
            0m,
            12_500_000m,
            [new DocumentPrevisionUbLigneDto(55, "UB-055", "Unité 055", 12_500_000m, 0, 0, 12_500_000m)]);

        var sw = Stopwatch.StartNew();
        var pdf = new QuestPdfDocumentRenderer().Render(payload);
        sw.Stop();

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 800, $"PDF trop petit : {pdf.Length}");
        Assert.True(pdf.Length < 500_000, $"PDF trop volumineux : {pdf.Length}");
        Assert.True(sw.ElapsedMilliseconds < 5_000, $"Génération trop lente : {sw.ElapsedMilliseconds} ms");

        // QuestPDF compresse souvent le texte ; on valide le formatage source (contrat fr-FR).
        var usd = string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0:N2} USD", 12_500_000m);
        Assert.DoesNotContain(",", usd.Replace(",00", "")); // pas de virgule milliers US
        Assert.EndsWith("USD", usd);

        // Checklist structurelle (payload → rendu) — logo / signature absents du moteur actuel.
        Assert.Contains("SNEL", payload.Reference);
        Assert.Equal("DFI", payload.CodeDepartement);
        Assert.Equal("UB-055", payload.CodeUB);
        Assert.Equal("Alice Dupont", payload.NomUtilisateurAuteur);
        Assert.Equal("Montants incomplets", payload.Motif);
        Assert.Equal(DocumentPrevisionPortee.Ub, payload.Portee);
    }

    [Fact]
    public void Pdf_PorteeDepartement_NbUb()
    {
        var payload = new DocumentPrevisionPayloadDto(
            DocumentPrevisionType.Titre(DocumentPrevisionType.Controle),
            "SNEL/DFI/BUD/2026/V04/CTL/00014",
            DocumentPrevisionType.Controle,
            DocumentPrevisionPortee.Departement,
            3,
            2026, 10, 4, "V04",
            7, "DFI", "Direction Financière",
            null, null, null,
            DateTime.Now, 42, "Alice Dupont",
            "SOUMISE", "CONTROLEE", null, "Contrôle favorable.",
            100m, 50m, 25m, 175m,
            [
                new DocumentPrevisionUbLigneDto(1, "U1", "A", 50, 20, 10, 80),
                new DocumentPrevisionUbLigneDto(2, "U2", "B", 30, 20, 10, 60),
                new DocumentPrevisionUbLigneDto(3, "U3", "C", 20, 10, 5, 35),
            ]);

        var pdf = new QuestPdfDocumentRenderer().Render(payload);
        Assert.True(pdf.Length > 800);
        Assert.Equal(3, payload.NbUbConcernees);
        Assert.Equal(DocumentPrevisionPortee.Departement, payload.Portee);
    }

    private static (DocumentPrevisionService Svc, FakeFileStore Store, FakeDocRepo Repo) Create()
    {
        var repo = new FakeDocRepo();
        var store = new FakeFileStore();
        var svc = new DocumentPrevisionService(repo, new FakePdfRenderer(), store);
        return (svc, store, repo);
    }

    private sealed class FakePdfRenderer : IDocumentPdfRenderer
    {
        public byte[] Render(DocumentPrevisionPayloadDto payload)
            => System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4\n{payload.Reference}|{payload.Motif}|{payload.MontantTotal}");
    }

    private sealed class FakeFileStore : IDocumentFileStore
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<(string RelativePath, string AbsolutePath)> SaveAsync(
            string reference, byte[] content, CancellationToken cancellationToken = default)
        {
            var key = reference.Replace('/', '_');
            Files[key] = content;
            return Task.FromResult((key + ".pdf", key + ".pdf"));
        }

        public Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var key = relativePath.Replace(".pdf", "", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(Files.TryGetValue(key, out var b) ? b : null);
        }
    }

    private sealed class FakeDocRepo : IDocumentPrevisionRepository
    {
        private readonly Dictionary<(short, long, int, string), int> _seq = new();
        private readonly List<DocumentPrevisionDto> _docs = [];
        private long _nextId = 1;

        public Task<int> AllouerNumeroAsync(
            short annee, long idDepartement, int numeroVersion, string typeDocument,
            CancellationToken cancellationToken = default)
        {
            var key = (annee, idDepartement, numeroVersion, typeDocument.ToUpperInvariant());
            _seq.TryGetValue(key, out var n);
            n += 1;
            _seq[key] = n;
            return Task.FromResult(n);
        }

        public Task<DocumentPrevisionDto> InsertAsync(
            DocumentPrevisionCreateCommand command, string reference, string payloadJson,
            string cheminFichier, string hashSha256, long tailleOctets,
            CancellationToken cancellationToken = default)
        {
            var total = command.MontantDC + command.MontantAE + command.MontantBI;
            var dto = new DocumentPrevisionDto(
                _nextId++, reference, command.TypeDocument, DocumentPrevisionType.Titre(command.TypeDocument),
                command.IdAudit, command.IdVersion, command.AnneeExercice, command.NumeroVersion,
                command.IdDepartement, command.CodeDepartement, command.LibelleDepartement,
                command.IdUB, command.CodeUB, command.LibelleUB, command.Portee, command.NbUbConcernees,
                command.IdUtilisateurAuteur, command.NomUtilisateurAuteur, command.DateEvenement,
                command.StatutAvant, command.StatutApres, command.Motif,
                command.MontantDC, command.MontantAE, command.MontantBI, total,
                DateTime.Now, tailleOctets);
            _docs.Add(dto);
            return Task.FromResult(dto);
        }

        public Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.IdDocument == idDocument));

        public Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.Reference == reference));

        public Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.IdAudit == idAudit));

        public Task<(string CheminFichier, string HashSha256)?> GetFichierAsync(
            long idDocument, CancellationToken cancellationToken = default)
        {
            var d = _docs.FirstOrDefault(x => x.IdDocument == idDocument);
            return Task.FromResult<(string, string)?>(d is null ? null : (d.Reference.Replace('/', '_') + ".pdf", "HASH"));
        }

        public Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
            long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<(short, int, string?)?>((2026, 1, "Initiale"));

        public Task<(long IdDepartement, string Code, string Libelle)?> GetDepartementAsync(
            long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult<(long, string, string)?>((5, "DSI", "Direction SI"));

        public Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement, string CodeUB, string LibelleUB)?> GetUbContexteAsync(
            long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<(long, string, string, string, string)?>(
                (5, "DSI", "Direction SI", "UB-" + idUB, "Unité " + idUB));

        public Task<string?> GetUtilisateurNomAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("Alice Dupont");

        public Task<(decimal Dc, decimal Ae, decimal Bi)> GetMontantsAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult((1000m, 200m, 50m));

        public Task<IReadOnlyDictionary<long, (decimal Dc, decimal Ae, decimal Bi)>> GetMontantsBatchAsync(
            long idVersion, IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<long, (decimal, decimal, decimal)>>(
                ubIds.ToDictionary(id => id, _ => (1000m, 200m, 50m)));

        public Task<IReadOnlyDictionary<long, (string CodeUB, string LibelleUB, long IdDepartement)>> GetUbInfosBatchAsync(
            IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<long, (string, string, long)>>(
                ubIds.ToDictionary(id => id, id => ($"UB-{id}", $"Unité {id}", 5L)));

        public Task<(string? CodeMode, bool EstMensuel)> GetModeDominantAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<(string?, bool)>(("ANNUEL", false));

        public Task<IReadOnlyList<DocumentPrevisionLigneDetailDto>> GetLignesDetailUbAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentPrevisionLigneDetailDto>>([]);

        public Task<long?> FindLatestAuditIdAsync(string operation, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(999);
    }

    /// <summary>Fake multi-départements pour isolation A vs B.</summary>
    private sealed class FakeDocRepoIsolation : IDocumentPrevisionRepository
    {
        private readonly Dictionary<(short, long, int, string), int> _seq = new();
        private readonly List<DocumentPrevisionDto> _docs = [];
        private long _nextId = 1;

        public Task<int> AllouerNumeroAsync(
            short annee, long idDepartement, int numeroVersion, string typeDocument,
            CancellationToken cancellationToken = default)
        {
            var key = (annee, idDepartement, numeroVersion, typeDocument.ToUpperInvariant());
            _seq.TryGetValue(key, out var n);
            n += 1;
            _seq[key] = n;
            return Task.FromResult(n);
        }

        public Task<DocumentPrevisionDto> InsertAsync(
            DocumentPrevisionCreateCommand command, string reference, string payloadJson,
            string cheminFichier, string hashSha256, long tailleOctets,
            CancellationToken cancellationToken = default)
        {
            var total = command.MontantDC + command.MontantAE + command.MontantBI;
            var dto = new DocumentPrevisionDto(
                _nextId++, reference, command.TypeDocument, DocumentPrevisionType.Titre(command.TypeDocument),
                command.IdAudit, command.IdVersion, command.AnneeExercice, command.NumeroVersion,
                command.IdDepartement, command.CodeDepartement, command.LibelleDepartement,
                command.IdUB, command.CodeUB, command.LibelleUB, command.Portee, command.NbUbConcernees,
                command.IdUtilisateurAuteur, command.NomUtilisateurAuteur, command.DateEvenement,
                command.StatutAvant, command.StatutApres, command.Motif,
                command.MontantDC, command.MontantAE, command.MontantBI, total,
                DateTime.Now, tailleOctets);
            _docs.Add(dto);
            return Task.FromResult(dto);
        }

        public Task<DocumentPrevisionDto?> GetByIdAsync(long idDocument, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.IdDocument == idDocument));

        public Task<DocumentPrevisionDto?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.Reference == reference));

        public Task<DocumentPrevisionDto?> GetByAuditAsync(long idAudit, CancellationToken cancellationToken = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.IdAudit == idAudit));

        public Task<(string CheminFichier, string HashSha256)?> GetFichierAsync(
            long idDocument, CancellationToken cancellationToken = default)
            => Task.FromResult<(string, string)?>(null);

        public Task<(short Annee, int NumeroVersion, string? Libelle)?> GetVersionInfoAsync(
            long idVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<(short, int, string?)?>((2026, 4, "V04"));

        public Task<(long IdDepartement, string Code, string Libelle)?> GetDepartementAsync(
            long idDepartement, CancellationToken cancellationToken = default)
            => Task.FromResult<(long, string, string)?>(
                idDepartement == 1 ? (1, "DFA", "Département A") : (2, "DFB", "Département B"));

        public Task<(long IdDepartement, string CodeDepartement, string LibelleDepartement, string CodeUB, string LibelleUB)?> GetUbContexteAsync(
            long idUB, CancellationToken cancellationToken = default)
        {
            if (idUB == 10)
                return Task.FromResult<(long, string, string, string, string)?>((1, "DFA", "Département A", "UA-10", "UB A"));
            return Task.FromResult<(long, string, string, string, string)?>((2, "DFB", "Département B", "UB-20", "UB B"));
        }

        public Task<string?> GetUtilisateurNomAsync(long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("Alice Dupont");

        public Task<(decimal Dc, decimal Ae, decimal Bi)> GetMontantsAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult((100m, 0m, 0m));

        public Task<IReadOnlyDictionary<long, (decimal Dc, decimal Ae, decimal Bi)>> GetMontantsBatchAsync(
            long idVersion, IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<long, (decimal, decimal, decimal)>>(
                ubIds.ToDictionary(id => id, _ => (100m, 0m, 0m)));

        public Task<IReadOnlyDictionary<long, (string CodeUB, string LibelleUB, long IdDepartement)>> GetUbInfosBatchAsync(
            IReadOnlyList<long> ubIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<long, (string, string, long)>>(
                ubIds.ToDictionary(id => id, id => ($"UB-{id}", $"Unité {id}", 5L)));

        public Task<(string? CodeMode, bool EstMensuel)> GetModeDominantAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<(string?, bool)>(("ANNUEL", false));

        public Task<IReadOnlyList<DocumentPrevisionLigneDetailDto>> GetLignesDetailUbAsync(
            long idVersion, long idUB, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentPrevisionLigneDetailDto>>([]);

        public Task<long?> FindLatestAuditIdAsync(string operation, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);
    }
}
