using ClosedXML.Excel;

var path = args.Length > 0 ? args[0] : "Correspondance_Organisationnelle_Budget_Web.xlsx";
using var wb = new XLWorkbook(path);
var arbre = wb.Worksheet("07_ARBRE_SOURCE");
var headers = arbre.FirstRowUsed()!.CellsUsed().Select(c => c.GetString().Trim()).ToList();
Console.WriteLine("Headers: " + string.Join(", ", headers));

var typeLignes = new HashSet<string>();
var typeElements = new HashSet<string>();
foreach (var row in arbre.RowsUsed().Skip(1))
{
    typeLignes.Add(row.Cell(6).GetString().Trim());
    typeElements.Add(row.Cell(7).GetString().Trim());
}
Console.WriteLine("TypeLigne: " + string.Join(", ", typeLignes.OrderBy(x => x)));
Console.WriteLine("TypeElement: " + string.Join(", ", typeElements.OrderBy(x => x)));

Console.WriteLine("\nSample rows without Code_UB:");
foreach (var row in arbre.RowsUsed().Skip(1).Where(r => string.IsNullOrWhiteSpace(r.Cell(12).GetString())).Take(5))
{
    Console.WriteLine($"L{row.Cell(1).GetString()} Ent={row.Cell(2).GetString()} Dep={row.Cell(4).GetString()} Type={row.Cell(7).GetString()} Lib={row.Cell(9).GetString()}");
}

Console.WriteLine("\nREADME:");
foreach (var row in wb.Worksheet("00_README").RowsUsed().Skip(1))
{
    Console.WriteLine($"{row.Cell(1).GetString()}: {row.Cell(2).GetString()}");
}
