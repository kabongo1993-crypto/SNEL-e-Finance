using System.Net;

namespace BudgetWeb.Mcp.Auth;

public static class OAuthLoginPage
{
    public static string Render(string ticket, string? error = null)
    {
        var err = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"<p class=\"err\">{WebUtility.HtmlEncode(error)}</p>";

        return $$"""
<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Connexion Budget Web</title>
  <style>
    body { font-family: Segoe UI, system-ui, sans-serif; background:#0f2744; color:#12263a; margin:0; }
    .card { max-width: 420px; margin: 8vh auto; background:#fff; border-radius:12px; padding:28px; box-shadow: 0 12px 40px rgba(0,0,0,.25); }
    h1 { font-size: 1.25rem; margin: 0 0 8px; color:#0f2744; }
    p { color:#445; font-size:.95rem; }
    label { display:block; margin:12px 0 4px; font-weight:600; font-size:.85rem; }
    input { width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #c9d4e3; border-radius:8px; }
    button { margin-top:18px; width:100%; padding:12px; border:0; border-radius:8px; background:#0b6e4f; color:#fff; font-weight:700; cursor:pointer; }
    .err { color:#a40000; background:#fdeaea; padding:8px 10px; border-radius:8px; }
    .hint { font-size:.8rem; color:#667; }
  </style>
</head>
<body>
  <form class="card" method="post" action="/oauth/authorize/login" autocomplete="on">
    <h1>Budget Web</h1>
    <p>Connectez-vous avec votre compte Budget Web pour autoriser ChatGPT à <strong>consulter</strong> vos données (lecture seule).</p>
    {{err}}
    <input type="hidden" name="ticket" value="{{WebUtility.HtmlEncode(ticket)}}"/>
    <label for="username">Nom d'utilisateur</label>
    <input id="username" name="username" required autofocus/>
    <label for="password">Mot de passe</label>
    <input id="password" name="password" type="password" required/>
    <button type="submit">Autoriser la lecture</button>
    <p class="hint">Les droits, le périmètre et les données restent ceux de votre compte Budget Web. Aucune écriture n'est autorisée dans cette version.</p>
  </form>
</body>
</html>
""";
    }
}
