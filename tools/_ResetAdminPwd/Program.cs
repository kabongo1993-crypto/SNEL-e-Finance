using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

var password = args.Length > 0 ? args[0] : "Admin@2026";
var user = "admin.snel";
var hasher = new PasswordHasher<object>();
var hash = hasher.HashPassword(new object(), password);

var cs = "Server=localhost\\HEROS_SQL19;Database=BD_SNEL;Trusted_Connection=True;TrustServerCertificate=True;";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new SqlCommand(
    "UPDATE UTILISATEUR SET MotDePasseHash = @h, Actif = 1 WHERE NomUtilisateur = @u; SELECT @@ROWCOUNT;", conn);
cmd.Parameters.AddWithValue("@h", hash);
cmd.Parameters.AddWithValue("@u", user);
var n = (int)(await cmd.ExecuteScalarAsync() ?? 0);
Console.WriteLine(n == 1 ? "RESET_OK" : $"RESET_FAIL rows={n}");
Console.WriteLine($"user={user}");
