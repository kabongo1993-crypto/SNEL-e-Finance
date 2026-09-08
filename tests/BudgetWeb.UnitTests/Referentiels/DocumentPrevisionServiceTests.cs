using BudgetWeb.Application.DTOs;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Application.Services;
using BudgetWeb.Domain.Enums;
using Xunit;

namespace BudgetWeb.UnitTests.Referentiels;

public class DocumentPrevisionServiceTests
{
    [Fact]
    public void ReferenceGenerator_InclutExerciceVersionTypeSequence()
    {
        var r = DocumentReferenceGenerator.Build("DSI", 2026, 1, DocumentPrevisionType.Soumission, 7);
        Assert.Equal("SNEL/DSI/BUD/2026/V01/SUB/00007", r);
    }

    [Fact]
    public async Task GenererSoumissionUb_ReferenceUnique_Utilisateur_Date_MontantsUsd()
    {
        var (svc, store, repo) = Create();
        var before = DateTime.Now.AddSeconds(-2);

        var doc = await svc.GenererSoumissionUbAsync(10, 100, 42, idAudit: 501);

        Assert.Equal(DocumentPrevisionType.Soumission, doc.TypeDocument);
        Assert.Equal(DocumentPrevisionPortee.Ub, doc.Portee);
        Assert.Equal(1, doc.NbUbConcernees);
        Assert.Equal(100, doc.IdUB);
        Assert.Equal("SNEL/DSI/BUD/2026/V01/SUB/00001", doc.Reference);
        Assert.Equal(501, doc.IdAudit);
        Assert.Equal(42, doc.IdUtilisateurAuteur);
        Assert.Equal("Alice Dupont", doc.NomUtilisateurAuteur);
        Assert.True(doc.DateEvenement >= before);
        Assert.Equal(1000m, doc.MontantDC);
        Assert.Equal(200m, doc.MontantAE);
        Assert.Equal(50m, doc.MontantBI);
        Assert.Equal(1250m, doc.MontantTotal);
        Assert.True(store.Files.ContainsKey(doc.Reference.Replace('/', '_')));
        Assert.NotNull(await svc.GetByAuditAsync(501));
        Assert.Equal(doc.Reference, (await svc.GetByReferenceAsync(doc.Reference))!.Reference);
    }

    [Fact]
    public async Task GenererSoumissionDepartement_ConserveUbEtNombre()
    {
        var (svc, _, _) = Create();
        var doc = await svc.GenererSoumissionDepartementAsync(10, 5, 42, 600, [100, 101]);

        Assert.NotNull(doc);
        Assert.Equal(DocumentPrevisionPortee.Departement, doc!.Portee);
        Assert.Equal(2, doc.NbUbConcernees);
        Assert.Null(doc.IdUB);
        Assert.Equal("DSI", doc.CodeDepartement);
        Assert.Equal(2000m, doc.MontantDC); // 1000+1000
        Assert.Contains("/SUB/", doc.Reference);
    }

    [Fact]
    public async Task GenererRejetUb_ExigeMotif_EtConserveMotif()
    {
        var (svc, _, _) = Create();
        var doc = await svc.GenererRejetUbAsync(
            10, 100, 42, "Montants incomplets", StatutVersionBudgetaire.Soumise, 701);

        Assert.Equal(DocumentPrevisionType.Rejet, doc.TypeDocument);
        Assert.Equal("Montants incomplets", doc.Motif);
        Assert.Equal(StatutVersionBudgetaire.Soumise, doc.StatutAvant);
        Assert.Equal(StatutVersionBudgetaire.Rejetee, doc.StatutApres);
        Assert.Contains("/REJ/", doc.Reference);
    }

    [Fact]
    public async Task GenererRejetUb_SansMotif_Echoue()
    {
        var (svc, _, _) = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GenererRejetUbAsync(10, 100, 42, "  ", StatutVersionBudgetaire.Soumise, 702));
    }

    [Fact]
    public async Task GenererRejetDepartement_AvecMotifEtUb()
    {
        var (svc, _, _) = Create();
        var doc = await svc.GenererRejetDepartementAsync(
            10, 5, 42, "Hors plafond", 703,
            [(100, StatutVersionBudgetaire.Soumise), (101, StatutVersionBudgetaire.Controlee)]);

        Assert.NotNull(doc);
        Assert.Equal(2, doc!.NbUbConcernees);
        Assert.Equal("Hors plafond", doc.Motif);
        Assert.Equal(DocumentPrevisionPortee.Departement, doc.Portee);
    }

    [Fact]
    public async Task GenererControle_Et_Validation()
    {
        var (svc, _, _) = Create();
        var ctl = await svc.GenererControleUbAsync(10, 100, 42, 801);
        var val = await svc.GenererValidationUbAsync(10, 100, 42, 802);

        Assert.Equal(DocumentPrevisionType.Controle, ctl.TypeDocument);
        Assert.Contains("/CTL/", ctl.Reference);
        Assert.Equal(DocumentPrevisionType.Validation, val.TypeDocument);
        Assert.Contains("/VAL/", val.Reference);
        Assert.NotEqual(ctl.Reference, val.Reference);
    }

    [Fact]
    public async Task GenererControleDepartement_Et_ValidationDepartement()
    {
        var (svc, _, _) = Create();
        var ctl = await svc.GenererControleDepartementAsync(10, 5, 42, 803, [100, 101]);
        var val = await svc.GenererValidationDepartementAsync(10, 5, 42, 804, [100]);

        Assert.NotNull(ctl);
        Assert.Equal(2, ctl!.NbUbConcernees);
        Assert.NotNull(val);
        Assert.Equal(1, val!.NbUbConcernees);
    }

    [Fact]
    public async Task Sequence_ProduitReferencesUniques()
    {
        var (svc, _, _) = Create();
        var a = await svc.GenererSoumissionUbAsync(10, 100, 42, 901);
        var b = await svc.GenererSoumissionUbAsync(10, 100, 42, 902);
        Assert.NotEqual(a.Reference, b.Reference);
        Assert.EndsWith("/00001", a.Reference);
        Assert.EndsWith("/00002", b.Reference);
    }

    [Fact]
    public async Task DepartementSansUbTraitee_NeGenerePas()
    {
        var (svc, _, _) = Create();
        var doc = await svc.GenererSoumissionDepartementAsync(10, 5, 42, 910, []);
        Assert.Null(doc);
    }

    [Fact]
    public async Task Pdf_EstGenereEtRelisible()
    {
        var (svc, store, _) = Create();
        var doc = await svc.GenererSoumissionUbAsync(10, 100, 42, 920);
        var bytes = await svc.GetPdfBytesAsync(doc.IdDocument);
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 10);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(store.Files.Values.Any(f => f.SequenceEqual(bytes)));
    }

    private static (DocumentPrevisionService Svc, FakeFileStore Store, FakeDocRepo Repo) Create()
    {
        var repo = new FakeDocRepo();
        var store = new FakeFileStore();
        var pdf = new FakePdfRenderer();
        var svc = new DocumentPrevisionService(repo, pdf, store);
        return (svc, store, repo);
    }

    private sealed class FakePdfRenderer : IDocumentPdfRenderer
    {
        public byte[] Render(DocumentPrevisionPayloadDto payload)
        {
            // En-tête PDF minimal + contenu utile pour assertions
            var body = System.Text.Encoding.UTF8.GetBytes(
                $"%PDF-1.4\n{payload.Reference}|{payload.NomUtilisateurAuteur}|{payload.MontantTotal}|{payload.Motif}");
            return body;
        }
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
            DocumentPrevisionCreateCommand command,
            string reference,
            string payloadJson,
            string cheminFichier,
            string hashSha256,
            long tailleOctets,
            CancellationToken cancellationToken = default)
        {
            var total = command.MontantDC + command.MontantAE + command.MontantBI;
            var dto = new DocumentPrevisionDto(
                _nextId++,
                reference,
                command.TypeDocument,
                DocumentPrevisionType.Titre(command.TypeDocument),
                command.IdAudit,
                command.IdVersion,
                command.AnneeExercice,
                command.NumeroVersion,
                command.IdDepartement,
                command.CodeDepartement,
                command.LibelleDepartement,
                command.IdUB,
                command.CodeUB,
                command.LibelleUB,
                command.Portee,
                command.NbUbConcernees,
                command.IdUtilisateurAuteur,
                command.NomUtilisateurAuteur,
                command.DateEvenement,
                command.StatutAvant,
                command.StatutApres,
                command.Motif,
                command.MontantDC,
                command.MontantAE,
                command.MontantBI,
                total,
                DateTime.Now,
                tailleOctets);
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
            if (d is null) return Task.FromResult<(string, string)?>(null);
            return Task.FromResult<(string, string)?>((d.Reference.Replace('/', '_') + ".pdf", "HASH"));
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

        public Task<long?> FindLatestAuditIdAsync(
            string operation, long idUtilisateur, CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(999);
    }
}
