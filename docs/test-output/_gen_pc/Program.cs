using System.Globalization;

using BudgetWeb.Application.DTOs;

using BudgetWeb.Infrastructure.Documents;



var dto = new PieceCaisseDocumentDto(

    TitreDocument: "PIECE DE CAISSE",

    NumeroPiece: "PC-4122-2026",

    DatePiece: new DateOnly(2026, 6, 19),

    ReferenceDemande: "R22",

    Motif:

        "FRAIS DE PARTICIPATION AU SEMINAIRE INTERNATIONAL SUR LA GESTION DES RESSOURCES HUMAINES A RABAT MAROC",

    PieceJustificative: "1956IP3164/025",

    BeneficiaireAffichage: "NESTORD",

    BeneficiaireMatricule: "38H38",

    BeneficiaireIdentite: null,

    MontantFc: 7_031_500m,

    MontantEnLettres: "SEPT MILLIONS TRENTE ET UN MILLE CINQ CENTS FRANCS CONGOLAIS",

    RecuSnel: "-",

    Sr: "78",

    ComptabiliteGenerale: "47110000",

    Cp: "P",

    Cpa: "A",

    NumeroAppariement: "5D20022",

    IdentifiantVerification: "PC-4122-2026-1",

    EtabliPar: "Charge DP Test",

    DateImpression: new DateTime(2026, 6, 19, 15, 0, 0));



var pdf = new QuestPdfPieceCaisseRenderer().Render(dto);

var outPath = Path.Combine("..", "piece-caisse-layout-v28-sample.pdf");

File.WriteAllBytes(outPath, pdf);

Console.WriteLine($"Wrote {Path.GetFullPath(outPath)} ({pdf.Length.ToString(CultureInfo.InvariantCulture)} bytes)");


