using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BudgetWeb.Infrastructure.Documents;

public sealed class QuestPdfWarmupHostedService : IHostedService
{
    private readonly QuestPdfWarmupOptions _options;
    private readonly ILogger<QuestPdfWarmupHostedService> _logger;

    public QuestPdfWarmupHostedService(
        IOptions<QuestPdfWarmupOptions> options,
        ILogger<QuestPdfWarmupHostedService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        if (_options.BlockStartup)
        {
            var ms = QuestPdfWarmup.Run();
            _logger.LogInformation("[QuestPDF warmup] termine en {ElapsedMs} ms (bloquant)", ms);
            return Task.CompletedTask;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var ms = QuestPdfWarmup.Run();
                _logger.LogInformation("[QuestPDF warmup] termine en {ElapsedMs} ms (arriere-plan)", ms);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[QuestPDF warmup] echec non bloquant");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}