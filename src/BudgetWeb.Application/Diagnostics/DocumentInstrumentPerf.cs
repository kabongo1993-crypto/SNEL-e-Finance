using System.Diagnostics;

namespace BudgetWeb.Application.Diagnostics;

/// <summary>Mesures intra-thread composer/QR pendant GeneratePdf (instrumentation temporaire).</summary>
public static class DocumentInstrumentPerf
{
    [ThreadStatic]
    private static long _composeDocumentMs;

    [ThreadStatic]
    private static long _qrMs;

    public static void Reset()
    {
        _composeDocumentMs = 0;
        _qrMs = 0;
    }

    public static void AddComposeDocument(TimeSpan elapsed)
        => _composeDocumentMs += (long)elapsed.TotalMilliseconds;

    public static void AddQr(TimeSpan elapsed)
        => _qrMs += (long)elapsed.TotalMilliseconds;

    public static (long ComposeDocumentMs, long QrMs) Snapshot()
        => (_composeDocumentMs, _qrMs);

    public static void TimeComposeDocument(Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            AddComposeDocument(sw.Elapsed);
        }
    }

    public static void TimeQr(Action action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            AddQr(sw.Elapsed);
        }
    }
}
