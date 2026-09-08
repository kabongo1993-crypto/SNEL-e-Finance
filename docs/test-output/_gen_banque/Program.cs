using ClosedXML.Excel;

var dest = args.Length > 0
    ? args[0]
    : Path.Combine("docs", "test-output", "Banque.xlsx");

var rows = new (string Id, string Libelle, string Pays)[]
{
    ("AFRILAND", "AFRILAND BANK", "Rdc"),
    ("ACCESSBANK", "ACCESS BANK", "Rdc"),
    ("BCC", "BCC", "Rdc"),
    ("BCDC", "BCDC", "Rdc"),
    ("BGFIBANK", "BGFIBANK", "Rdc"),
    ("RAWBANK", "RAWBANK", "Rdc"),
    ("TMB", "TRUST MERCHANT BANK", "Rdc"),
    ("ECOBANK", "ECOBANK RDC", "Rdc"),
    ("SOFIBANQUE", "SOFIBANQUE", "Rdc"),
    ("FBNBANK", "FBN BANK DRC", "Rdc"),
    ("CITIBANK", "CITIBANK RDC", "Rdc"),
    ("SCB", "STANDARD CHARTERED BANK", "Rdc"),
    ("UBA", "UNITED BANK FOR AFRICA", "Rdc"),
    ("BOA", "BANK OF AFRICA RDC", "Rdc"),
    ("BYBLOS", "BYBLOS BANK", "Rdc"),
    ("PROCREDIT", "PROCREDIT BANK", "Rdc"),
    ("ADVANS", "ADVANS BANQUE CONGO", "Rdc"),
    ("FIRSTBANK", "FIRST BANK DRC", "Rdc"),
    ("EQUITY", "EQUITY BCDC", "Rdc"),
    ("STANBIC", "STANBIC BANK", "Rdc"),
    ("BIC", "BANQUE INTERNATIONALE DE CREDIT", "Rdc"),
    ("IBANK", "I&M BANK", "Rdc"),
    ("SOFICOM", "SOFICOM", "Rdc"),
    ("CAISSESNEL", "CAISSE SNEL", "Rdc"),
    ("MINES", "BANQUE MINES", "Rdc"),
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dest))!);

using var wb = new XLWorkbook();
var ws = wb.AddWorksheet("Banques");
ws.Cell(1, 1).Value = "N° Enr.";
ws.Cell(1, 2).Value = "ID_Banque";
ws.Cell(1, 3).Value = "Libelle_Banque";
ws.Cell(1, 4).Value = "Pays";

for (var i = 0; i < rows.Length; i++)
{
    ws.Cell(i + 2, 1).Value = i + 1;
    ws.Cell(i + 2, 2).Value = rows[i].Id;
    ws.Cell(i + 2, 3).Value = rows[i].Libelle;
    ws.Cell(i + 2, 4).Value = rows[i].Pays;
}

wb.SaveAs(dest);
Console.WriteLine($"Écrit {rows.Length} banques → {Path.GetFullPath(dest)}");
