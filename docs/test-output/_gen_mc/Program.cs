using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;

var dto = new MinuteChequeDocumentDto(
    TitreDocument: "MINUTE DE CHÈQUE",
    NumeroOp: "OP-2026-00042",
    DateDocument: new DateOnly(2026, 6, 19),
    ReferenceDemande: "DP-2026-00018",
    Motif:
        "FRAIS DE PARTICIPATION AU SEMINAIRE INTERNATIONAL SUR LA GESTION DES RESSOURCES HUMAINES ET LE DEVELOPPEMENT DURABLE A RABAT MAROC POUR LE COMPTE DE LA DIRECTION DES FINANCES",
    BeneficiaireAffichage: "SOCIETE CONGOLESE DE CONSTRUCTION ET D'INGENIERIE INDUSTRIELLE SARL",
    BeneficiaireAdresse:
        "1234 AVENUE DES MARTYRS DE LA INDEPENDANCE NATIONALE, COMMUNE DE LA GOMBE, KINSHASA, REPUBLIQUE DEMOCRATIQUE DU CONGO",
    BeneficiaireBanque: "RAWBANK",
    BeneficiaireNumeroCompte: "001-0951851-03",
    MontantPaiement: 12_345_678.50m,
    DevisePaiement: "CDF",
    MontantEnLettres:
        "DOUZE MILLIONS TROIS CENT QUARANTE-CINQ MILLE SIX CENT SOIXANTE-DIX-HUIT FRANCS CONGOLAIS ET CINQUANTE CENTIMES",
    CompteGeneral: "47110000",
    CpCa: "PA",
    Ls: "L",
    SuiviExtraComptable: "SEC001234567",
    NumeroAppariement: "5D20022",
    MontantSuiviExtraComptable: 12_345_678.50m,
    IdentifiantVerification: "OP-2026-00042-1",
    EtabliPar: "Charge DP Test",
    DateImpression: new DateTime(2026, 6, 19, 15, 0, 0));

var pdf = new QuestPdfMinuteChequeRenderer().Render(dto);
var renderer = new QuestPdfMinuteChequeRenderer();

var outDir = Path.GetFullPath(Path.Combine(".."));
var pdfPath = Path.Combine(outDir, "minute-cheque-layout-v2-sample.pdf");
File.WriteAllBytes(pdfPath, pdf);
Console.WriteLine($"Wrote {pdfPath} ({pdf.Length.ToString(CultureInfo.InvariantCulture)} bytes)");

var pngPath = Path.Combine(outDir, "minute-cheque-layout-v2-review.png");
File.WriteAllBytes(pngPath, renderer.RenderPreviewPng(dto));
Console.WriteLine($"Wrote {pngPath}");
