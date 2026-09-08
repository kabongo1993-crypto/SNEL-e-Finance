# Déploiement — Budget Web MCP (ChatGPT)

MCP v1 = **instance unique**, lecture seule, identité Budget Web. Pas de Redis / SQL OAuth. Redémarrage = OAuth à refaire. Pas de refresh token : expiration JWT → nouvel OAuth ChatGPT.

Aucun secret dans Git.

## Procédure

### 1. Publier BudgetWeb.API

Déployer l’API existante. Noter l’URL interne (ex. `https://api-budget.interne`) et le secret JWT **déjà** utilisé par l’API (`Jwt__SecretKey`, `Jwt__Issuer`, `Jwt__Audience`).

### 2. Publier BudgetWeb.Mcp

```powershell
dotnet publish src/BudgetWeb.Mcp/BudgetWeb.Mcp.csproj -c Release -o C:\apps\BudgetWeb.Mcp
```

Service Windows / systemd : `BudgetWeb.Mcp.dll`. Une **seule** instance. Écouter en HTTP local derrière le reverse proxy, ex. `ASPNETCORE_URLS=http://127.0.0.1:5260`.

### 3. Variables d’environnement

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5260
Mcp__PublicBaseUrl=https://mcp.mon-domaine.com
Mcp__ApiBaseUrl=https://api-budget.interne
Mcp__ApiTimeoutSeconds=30
Jwt__SecretKey=<secret HMAC identique à BudgetWeb.API, jamais la clé DEV>
Jwt__Issuer=BudgetWeb-SNEL
Jwt__Audience=BudgetWeb-Client
```

Si le reverse proxy n’est **pas** sur loopback :

```text
Mcp__TrustedProxies__0=<IP du proxy>
Mcp__TrustedNetworks__0=<CIDR du proxy, ex. 10.0.0.0/8>
```

Sans cette liste, seuls les proxies loopback sont crus. Les Forwarded Headers d’Internet sont ignorés.

Le démarrage **échoue** si :

- `Jwt__SecretKey` est la clé DEV (`DEV-ONLY-…`) ou une clé « ChangeInProd » ;
- `Mcp__PublicBaseUrl` est vide ou n’est pas `https://…`.

### 4. HTTPS

Exposer ChatGPT **uniquement** via HTTPS sur `Mcp__PublicBaseUrl`. Reverse proxy (IIS / nginx / Traefik) → `http://127.0.0.1:5260`.

Ne pas exposer le port HTTP du MCP sur Internet. En Production, une requête HTTP non-loopback est rejetée.

Le proxy ne doit **pas** réécrire `/.well-known/oauth-authorization-server/mcp`.

Exemple nginx :

```nginx
location / {
    proxy_pass http://127.0.0.1:5260;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto https;
    proxy_set_header X-Forwarded-Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
```

### 5. Vérifier `/health`

```powershell
curl https://mcp.mon-domaine.com/health
```

Attendu : `{"status":"ok",...}`

### 6. Vérifier OAuth metadata

```powershell
curl https://mcp.mon-domaine.com/.well-known/oauth-authorization-server
curl https://mcp.mon-domaine.com/.well-known/oauth-protected-resource
```

`issuer` = `Mcp__PublicBaseUrl`. Pas de CIMD, pas d’OpenID (`/.well-known/openid-configuration` = 404).

### 7. Vérifier `/mcp`

```powershell
curl -i https://mcp.mon-domaine.com/mcp
```

Attendu : **401** + `WWW-Authenticate` avec `resource_metadata`.

### 8. Connecter ChatGPT au MCP

Settings → Apps / Connectors → Developer mode → URL : `https://mcp.mon-domaine.com/mcp`.

Redirects autorisés :

- `https://chatgpt.com/connector_platform_oauth_redirect`
- `https://chatgpt.com/connector/oauth/*`

Enregistrement : DCR (`/oauth/register`).

### 9. Se connecter avec un vrai compte Budget Web

Login = identifiants Budget Web (pas un compte MCP). Le JWT est celui de l’API.

### 10. Tester un utilisateur restreint

Compte **non admin**, permissions réelles.

### 11. Tester une UB autorisée

Question métier sur une UB du périmètre → données visibles.

### 12. Tester une UB hors périmètre

UB non accessible → refus, **pas** de totaux.

### 13. Tester une DPM non autorisée

DPM hors droits / hors périmètre → refus.

### 14. Vérifier l’isolation

Aucune donnée d’un autre utilisateur ne doit apparaître. Les droits restent ceux de Budget Web.

## Développement local

HTTP loopback autorisé (`ASPNETCORE_ENVIRONMENT=Development`, port 5260). Tunnel HTTPS + `Mcp__PublicBaseUrl` pour un test ChatGPT depuis le poste.

```powershell
dotnet run --project src/BudgetWeb.API --launch-profile http
dotnet run --project src/BudgetWeb.Mcp --launch-profile http
```
