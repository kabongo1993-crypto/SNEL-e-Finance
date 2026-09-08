namespace BudgetWeb.Infrastructure.Documents;

public sealed class QuestPdfWarmupOptions
{
    public const string SectionName = "QuestPdf:Warmup";

    public bool Enabled { get; set; }

    public bool BlockStartup { get; set; }
}