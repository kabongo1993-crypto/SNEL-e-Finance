# Intégration ChatGPT ↔ Budget Web (MCP)

Serveur MCP **lecture seule** pour ChatGPT (Streamable HTTP + OAuth 2.1 / PKCE). Identité = compte Budget Web existant. Aucun SQL, aucun utilisateur MCP générique, aucune règle métier dupliquée.

```text
Utilisateur ChatGPT
        ↓  OAuth 2.1 + PKCE (login Budget Web)
ChatGPT (connecteur MCP)
        ↓  Authorization: Bearer <JWT Budget Web>
BudgetWeb.Mcp  (port local 5260)
        ↓  HTTP + même JWT
BudgetWeb.API  → permissions, périmètre, règles métier existantes
```

## Instance unique (v1)

- **Une seule instance MCP.** Pas de load balancing multi-instance : clients OAuth, tickets, codes et refresh tokens sont **en mémoire**.
- Un redémarrage MCP **perd** les sessions OAuth temporaires (DCR, codes, refresh). ChatGPT doit refaire OAuth.
- **Refresh token opaque** (pas le JWT Budget Web). Émis seulement si le client demande `offline_access`. L’access token reste le JWT Budget Web.
- Le JWT n’est conservé en mémoire que le temps d’échanger le code (`authorization_code`, TTL 2 minutes) puis, si `offline_access`, associé au refresh token hashé. Jamais écrit dans un fichier ni dans les logs.

## Authentification

1. ChatGPT découvre `/.well-known/oauth-protected-resource` (et `/mcp/.well-known/oauth-protected-resource`) puis `/.well-known/oauth-authorization-server`.
2. Un `GET`/`POST` `/mcp` sans Bearer répond **401** avec `WWW-Authenticate: Bearer resource_metadata="…"`.
3. Enregistrement client : **DCR uniquement** (`POST /oauth/register`). Le serveur **n’annonce pas** CIMD et **n’implémente pas** OpenID Connect (`/.well-known/openid-configuration` n’existe pas).
4. Login Budget Web sur `/oauth/authorize` → `POST /api/v1/auth/login`.
5. Le jeton d’accès renvoyé à ChatGPT **est** le JWT Budget Web (`uid` = `IdUtilisateur`). Un refresh token opaque est émis si `offline_access` est demandé. `client_id` obligatoire à `/oauth/token`. PKCE S256 obligatoire. Code à usage unique.
6. Redirect URI : allowlist ChatGPT. Loopback **uniquement en Development**.

## Outils (lecture seule)

`get_budget_situation`, `get_credits_disponibles`, `get_budget_execution`, `get_engagements`, `get_imputations`, `search_demandes_paiement`, `get_demande_paiement`, `get_previsions`, `get_rapport_previsions`, `list_referentiels`.

Pas d’`execute_sql`. Pagination `take` max 50.

## Endpoints

| Méthode | Chemin | Auth |
|---|---|---|
| GET | `/health` | anonyme |
| GET | `/.well-known/oauth-authorization-server` (+ `/mcp` et `/mcp/.well-known/…`) | anonyme |
| GET | `/.well-known/oauth-protected-resource` (+ `/mcp` et `/mcp/.well-known/…`) | anonyme |
| POST | `/oauth/register` | anonyme (DCR) |
| GET | `/oauth/authorize` | anonyme (login) |
| POST | `/oauth/authorize/login` | anonyme |
| POST | `/oauth/token` | anonyme (code + PKCE + `client_id`, ou `refresh_token`) |
| GET/POST | `/mcp` | **JWT Budget Web** (sans Bearer : 401 + `WWW-Authenticate`) |

## Configuration

`appsettings.json` ne contient **aucun secret**. En Development, `appsettings.Development.json` peut contenir la clé JWT DEV. En Production : `Jwt__SecretKey` via l’environnement (identique à l’API). La clé `DEV-ONLY-…` est **refusée au démarrage** en Production.

`Mcp:PublicBaseUrl` est **obligatoire en Production** (HTTPS, ex. `https://mcp.mon-domaine.com`). Elle n’est jamais déduite de `Host` / Forwarded Headers.

Forwarded Headers : seulement loopback (défaut ASP.NET) **plus** `Mcp:TrustedProxies` / `Mcp:TrustedNetworks`. Ne pas faire confiance à un header venant d’Internet.

## Variables

| Variable | Usage |
|---|---|
| `Jwt__SecretKey` | Secret HMAC identique à l’API (≥ 32 car., jamais la clé DEV en Production) |
| `Jwt__Issuer` | `BudgetWeb-SNEL` |
| `Jwt__Audience` | `BudgetWeb-Client` |
| `Mcp__ApiBaseUrl` | URL interne de l’API |
| `Mcp__PublicBaseUrl` | URL HTTPS publique du MCP (obligatoire en Production) |
| `Mcp__ApiTimeoutSeconds` | Timeout HTTP vers l’API |
| `Mcp__TrustedProxies__0` | IP du reverse proxy (si hors loopback) |
| `Mcp__TrustedNetworks__0` | CIDR du reverse proxy (si hors loopback) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |

Déploiement : [ChatGPT-MCP-DEPLOYMENT.md](ChatGPT-MCP-DEPLOYMENT.md).

## Tests

```powershell
dotnet test tests/BudgetWeb.Mcp.Tests/BudgetWeb.Mcp.Tests.csproj
```
