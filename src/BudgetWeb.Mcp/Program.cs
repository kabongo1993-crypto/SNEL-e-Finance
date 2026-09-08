using System.Net;
using System.Text;
using BudgetWeb.Mcp.Auth;
using BudgetWeb.Mcp.Client;
using BudgetWeb.Mcp.Options;
using BudgetWeb.Mcp.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

McpStartupGuard.Validate(builder.Environment, builder.Configuration);
var jwtSecret = builder.Configuration["Jwt:SecretKey"]!;

builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    var mcp = new McpOptions();
    builder.Configuration.GetSection(McpOptions.SectionName).Bind(mcp);
    McpOptions.ApplyTrustedForwarders(options, mcp);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<OAuthClientStore>();
builder.Services.AddSingleton<IOAuthAuthorizationStore, OAuthAuthorizationStore>();
builder.Services.AddTransient<BearerForwardingHandler>();

builder.Services.AddHttpClient<BudgetWebApiClient>((sp, client) =>
{
    var api = builder.Configuration["Mcp:ApiBaseUrl"] ?? "http://localhost:5257";
    client.BaseAddress = new Uri(api.TrimEnd('/') + "/");
    var timeout = 30;
    if (int.TryParse(builder.Configuration["Mcp:ApiTimeoutSeconds"], out var t) && t > 0)
        timeout = t;
    client.Timeout = TimeSpan.FromSeconds(timeout);
}).AddHttpMessageHandler<BearerForwardingHandler>();

builder.Services.AddHttpClient("BudgetWebLogin", (sp, client) =>
{
    var api = builder.Configuration["Mcp:ApiBaseUrl"] ?? "http://localhost:5257";
    client.BaseAddress = new Uri(api.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                var env = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                var mcp = context.HttpContext.RequestServices.GetRequiredService<IOptions<McpOptions>>().Value;
                var publicBase = OAuthEndpoints.PublicBase(context.HttpContext, mcp, env);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = OAuthEndpoints.WwwAuthenticate(publicBase);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatGpt", policy =>
    {
        policy.WithOrigins(
                "https://chatgpt.com",
                "https://chat.openai.com",
                "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("WWW-Authenticate");
    });
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Budget Web",
            Version = "1.0.0"
        };
        options.ServerInstructions =
            "Serveur MCP lecture seule de Budget Web. Identité = utilisateur Budget Web (JWT). " +
            "Respecte permissions et périmètre. Aucune écriture. Aucun SQL.";
    })
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Erreur interne." });
        });
    });
}

app.UseForwardedHeaders();

if (app.Environment.IsProduction())
{
    app.Use(async (ctx, next) =>
    {
        var remote = ctx.Connection.RemoteIpAddress;
        var isLoopback = remote is null
            || IPAddress.IsLoopback(remote)
            || (remote.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(remote.MapToIPv4()));
        if (!ctx.Request.IsHttps && !isLoopback)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "https_required",
                message = "Le MCP n'accepte que HTTPS en Production (hors loopback)."
            });
            return;
        }

        await next();
    });
}

app.UseCors("ChatGpt");
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    var isMcpDiscovery = ctx.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase)
        && (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method));
    if (isMcpDiscovery && ctx.User.Identity?.IsAuthenticated != true)
    {
        var env = ctx.RequestServices.GetRequiredService<IHostEnvironment>();
        var mcp = ctx.RequestServices.GetRequiredService<IOptions<McpOptions>>().Value;
        var publicBase = OAuthEndpoints.PublicBase(ctx, mcp, env);
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.Headers.WWWAuthenticate = OAuthEndpoints.WwwAuthenticate(publicBase);
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    product = "BudgetWeb.Mcp",
    mode = "read-only"
})).AllowAnonymous();

app.MapGet("/", (HttpContext http, IOptions<McpOptions> mcp, IHostEnvironment env) =>
{
    var issuer = OAuthEndpoints.PublicBase(http, mcp.Value, env);
    return Results.Json(new
    {
        name = "Budget Web MCP",
        mode = "read-only",
        mcp = issuer + "/mcp",
        oauth = issuer + "/.well-known/oauth-authorization-server",
        documentation = "docs/ChatGPT-MCP.md"
    });
}).AllowAnonymous();

app.MapGet("/.well-known/openid-configuration", () => Results.NotFound()).AllowAnonymous();
app.MapOAuth();
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

public partial class Program;
