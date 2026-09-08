using System.Text.Json;
using BudgetWeb.Application.Interfaces;
using BudgetWeb.Domain.Entities;
using BudgetWeb.Infrastructure.Persistence;

namespace BudgetWeb.Infrastructure.Repositories;

public sealed class McpAuditRepository : IMcpAuditRepository
{
    private readonly BudgetDbContext _context;

    public McpAuditRepository(BudgetDbContext context)
    {
        _context = context;
    }

    public async Task AjouterLectureAsync(
        long idUtilisateur,
        string outil,
        string? parametresFonctionnels,
        int? nombreResultats,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            outil,
            parametres = parametresFonctionnels,
            nombreResultats
        });

        _context.JournalAudits.Add(new JournalAudit
        {
            FK_Utilisateur = idUtilisateur,
            DateHeure = DateTime.Now,
            Operation = "MCP_READ",
            Entite = "MCP",
            IdEntite = 0,
            NouvellesValeurs = payload,
            AdresseIP = adresseIp
        });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
