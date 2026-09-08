namespace BudgetWeb.API.Models;

public class ApiErrorResponse
{
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public object? Remplacement { get; set; }
}
