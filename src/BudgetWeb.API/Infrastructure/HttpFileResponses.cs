using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BudgetWeb.API.Infrastructure;

/// <summary>Réponses fichier avec Content-Disposition explicite (inline vs attachment).</summary>
public static class HttpFileResponses
{
    public static FileStreamResult Attachment(Stream content, string contentType, string fileName)
        => new(content, contentType)
        {
            FileDownloadName = SanitizeFileName(fileName),
            EnableRangeProcessing = true,
        };

    public static FileStreamResult Inline(Stream content, string contentType, string fileName)
        => new InlineFileStreamResult(content, contentType, SanitizeFileName(fileName));

    public static FileContentResult Attachment(byte[] content, string contentType, string fileName)
        => new(content, contentType) { FileDownloadName = SanitizeFileName(fileName) };

    public static FileContentResult Inline(byte[] content, string contentType, string fileName)
        => new InlineFileContentResult(content, contentType, SanitizeFileName(fileName));

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrWhiteSpace(name))
            return "document";
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c is '"' or '\r' or '\n')
                continue;
            sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString() : "document";
    }

    private sealed class InlineFileContentResult : FileContentResult
    {
        private readonly string _fileName;

        public InlineFileContentResult(byte[] content, string contentType, string fileName)
            : base(content, contentType)
            => _fileName = fileName;

        public override Task ExecuteResultAsync(ActionContext context)
        {
            SetInlineDisposition(context, _fileName);
            return base.ExecuteResultAsync(context);
        }
    }

    private sealed class InlineFileStreamResult : FileStreamResult
    {
        private readonly string _fileName;

        public InlineFileStreamResult(Stream content, string contentType, string fileName)
            : base(content, contentType)
            => _fileName = fileName;

        public override Task ExecuteResultAsync(ActionContext context)
        {
            SetInlineDisposition(context, _fileName);
            return base.ExecuteResultAsync(context);
        }
    }

    private static void SetInlineDisposition(ActionContext context, string fileName)
    {
        var cd = new ContentDispositionHeaderValue("inline");
        cd.SetHttpFileName(fileName);
        context.HttpContext.Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();
    }
}
