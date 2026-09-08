using System.Diagnostics;

using Microsoft.Extensions.Logging;



namespace BudgetWeb.Application.Diagnostics;



/// <summary>Instrumentation temporaire Lot perf documents — AsyncLocal, sans effet métier.</summary>

public sealed class DocumentPerfScope : IDisposable

{

    private static readonly AsyncLocal<DocumentPerfScope?> CurrentScope = new();



    public static DocumentPerfScope? Current => CurrentScope.Value;



    private readonly ILogger _logger;

    private readonly string _tag;

    private readonly long _demandeId;

    private readonly Stopwatch _total = Stopwatch.StartNew();



    public long GetDetailMs { get; set; }

    public long PiecesObligatoiresMs { get; set; }

    public long MappingMs { get; set; }

    public long FallbackMs { get; set; }

    public long SqlMs { get; set; }

    public long AccessMs { get; set; }

    public long QuestPdfMs { get; set; }

    public long AuditMs { get; set; }

    public long GetForPieceUploadMs { get; set; }

    public long ReadStreamMs { get; set; }

    public long MemoryStreamMs { get; set; }

    public long Sha256Ms { get; set; }

    public long StorageMs { get; set; }

    public long ObligatoireMs { get; set; }

    public long EmpreinteCollectionsMs { get; set; }

    public long SaveChangesMs { get; set; }

    public long GetDemandeAccessMs { get; set; }

    public long GetPdfDataMs { get; set; }

    public long GetPieceMs { get; set; }

    public long OpenFileMs { get; set; }



    private DocumentPerfScope(ILogger logger, string tag, long demandeId)

    {

        _logger = logger;

        _tag = tag;

        _demandeId = demandeId;

        CurrentScope.Value = this;

    }



    public static DocumentPerfScope Begin(ILogger logger, string tag, long demandeId)

        => new(logger, tag, demandeId);



    public void LogDpmPdf(int pdfSizeBytes)

    {

        _logger.LogInformation(

            "[PERF][DPM-PDF] DemandeId={DemandeId} GetAccesContext={GetDemandeAccessMs}ms " +

            "GarantirAcces={AccessMs}ms GetDemandePaiementPdfData={GetPdfDataMs}ms Mapping={MappingMs}ms " +

            "QuestPDF={QuestPdfMs}ms Audit={AuditMs}ms Total={TotalMs}ms PdfSize={PdfSizeKb}KB",

            _demandeId, GetDemandeAccessMs, AccessMs, GetPdfDataMs, MappingMs, QuestPdfMs, AuditMs,

            _total.ElapsedMilliseconds, pdfSizeBytes / 1024);

    }



    public void LogBilletPdf(int pdfSizeBytes)

    {

        _logger.LogInformation(

            "[PERF][BILLET-CONVERSION-PDF] DemandeId={DemandeId} GetAccesContext={GetDemandeAccessMs}ms " +

            "GarantirAcces={AccessMs}ms GetBilletConversionPdfData={GetPdfDataMs}ms Mapping={MappingMs}ms " +

            "QuestPDF={QuestPdfMs}ms Total={TotalMs}ms PdfSize={PdfSizeKb}KB",

            _demandeId, GetDemandeAccessMs, AccessMs, GetPdfDataMs, MappingMs, QuestPdfMs,

            _total.ElapsedMilliseconds, pdfSizeBytes / 1024);

    }



    public void LogInstrumentPdf(string tag, long qrMs, long composeMs, int pdfSizeBytes)

    {

        var composerMs = Math.Max(0, composeMs - qrMs);

        _logger.LogInformation(

            "[PERF][{Tag}] DemandeId={DemandeId} Sql={SqlMs}ms Mapping={MappingMs}ms Qr={QrMs}ms " +

            "Composer={ComposerMs}ms QuestPDF={QuestPdfMs}ms Total={TotalMs}ms PdfSize={PdfSizeKb}KB",

            tag, _demandeId, GetDetailMs + AccessMs, MappingMs, qrMs, composerMs, QuestPdfMs,

            _total.ElapsedMilliseconds, pdfSizeBytes / 1024);

    }



    public void LogPdfPipeline(string tag, long qrMs, long composeMs, int pdfSizeBytes, string pdfDataLabel)

    {

        var composerMs = Math.Max(0, composeMs - qrMs);

        _logger.LogInformation(

            "[PERF][{Tag}] DemandeId={DemandeId} GetAccesContext={GetDemandeAccessMs}ms " +

            "GarantirAcces={AccessMs}ms {PdfDataLabel}={GetPdfDataMs}ms Mapping={MappingMs}ms " +

            "Qr={QrMs}ms Composer={ComposerMs}ms QuestPDF={QuestPdfMs}ms Total={TotalMs}ms PdfSize={PdfSizeKb}KB",

            tag, _demandeId, GetDemandeAccessMs, AccessMs, pdfDataLabel, GetPdfDataMs, MappingMs,

            qrMs, composerMs, QuestPdfMs, _total.ElapsedMilliseconds, pdfSizeBytes / 1024);

    }



    public void LogPieceUpload(string fileName, long sizeBytes, string extension)

    {

        _logger.LogInformation(

            "[PERF][PIECE-UPLOAD] DemandeId={DemandeId} FileName={FileName} Size={SizeKb}KB Extension={Extension} " +

            "GetForPieceUpload={GetForPieceUploadMs}ms ReadStream={ReadStreamMs}ms MemoryStream={MemoryStreamMs}ms SHA256={Sha256Ms}ms " +

            "Storage={StorageMs}ms Obligatoire={ObligatoireMs}ms EmpreinteCollections={EmpreinteCollectionsMs}ms " +

            "SaveChanges={SaveChangesMs}ms Audit={AuditMs}ms Total={TotalMs}ms BufferSize={BufferKb}KB",

            _demandeId, fileName, sizeBytes / 1024, extension,

            GetForPieceUploadMs, ReadStreamMs, MemoryStreamMs, Sha256Ms, StorageMs, ObligatoireMs,

            EmpreinteCollectionsMs, SaveChangesMs, AuditMs, _total.ElapsedMilliseconds, sizeBytes / 1024);

    }



    public void LogPiecePreview(long? sizeBytes)

    {

        _logger.LogInformation(

            "[PERF][PIECE-PREVIEW] DemandeId={DemandeId} GetDemandeAccess={GetDemandeAccessMs}ms " +

            "GetPiece={GetPieceMs}ms OpenFile={OpenFileMs}ms Total={TotalMs}ms FileSize={SizeKb}KB",

            _demandeId, GetDemandeAccessMs, GetPieceMs, OpenFileMs, _total.ElapsedMilliseconds,

            sizeBytes is long s ? s / 1024 : 0);

    }



    public void LogPieceCaissePdf(long qrMs, long composeMs, int pdfSizeBytes)

        => LogPdfPipeline("PIECE-CAISSE-PDF", qrMs, composeMs, pdfSizeBytes, "GetPieceCaissePdfData");



    public void Dispose()

    {

        if (ReferenceEquals(CurrentScope.Value, this))

            CurrentScope.Value = null;

    }

}


