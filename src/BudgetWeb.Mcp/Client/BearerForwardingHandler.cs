using System.Net.Http.Headers;

namespace BudgetWeb.Mcp.Client;

/// <summary>Transmet le Bearer JWT Budget Web de la requête ChatGPT vers l'API.</summary>
public sealed class BearerForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public BearerForwardingHandler(IHttpContextAccessor http)
    {
        _http = http;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = _http.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth) && AuthenticationHeaderValue.TryParse(auth, out var header))
            request.Headers.Authorization = header;
        return base.SendAsync(request, cancellationToken);
    }
}
