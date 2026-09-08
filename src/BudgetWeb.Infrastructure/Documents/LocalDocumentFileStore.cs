using BudgetWeb.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BudgetWeb.Infrastructure.Documents;

public class LocalDocumentFileStore : IDocumentFileStore
{
    private readonly string _root;

    public LocalDocumentFileStore(IConfiguration configuration)
    {
        _root = configuration["Documents:PrevisionsPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "documents", "previsions");
        Directory.CreateDirectory(_root);
    }

    public Task<(string RelativePath, string AbsolutePath)> SaveAsync(
        string reference,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var safe = reference.Replace('/', '_').Replace('\\', '_');
        var year = DateTime.Now.Year.ToString();
        var dir = Path.Combine(_root, year);
        Directory.CreateDirectory(dir);
        var fileName = $"{safe}.pdf";
        var absolute = Path.Combine(dir, fileName);
        File.WriteAllBytes(absolute, content);
        var relative = Path.Combine(year, fileName).Replace('\\', '/');
        return Task.FromResult((relative, absolute));
    }

    public Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolute = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
        {
            return Task.FromResult<byte[]?>(null);
        }

        return Task.FromResult<byte[]?>(File.ReadAllBytes(absolute));
    }
}
