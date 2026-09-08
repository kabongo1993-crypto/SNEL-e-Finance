using System.Globalization;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Domain.DemandePaiement;
using BudgetWeb.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BudgetWeb.Infrastructure.Documents;

/// <summary>Mise en page officielle — Demande de Paiement e-Finance (1 page A4).</summary>
internal static class DemandePaiementPdfComposer
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly string AccentBlue = Colors.Blue.Darken3;
    private static readonly string AccentLine = Colors.Blue.Darken2;
    private static readonly string Muted = Colors.Grey.Darken2;
    private static readonly string Border = Colors.Grey.Lighten2;

    public static void ApplyPageChrome(PageDescriptor page, DemandePaiementDocumentDto payload)
    {
        page.Size(PageSizes.A4);
        page.MarginLeft(32);
        page.MarginRight(32);
        page.MarginTop(22);
        page.MarginBottom(38);
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Black));

        DemandePaiementPdfAssets.ApplyDiscreteBackgroundWatermark(page);
        page.Header().Element(h => ComposeHeader(h, payload));
        page.Footer().Element(f => ComposeFooter(f, payload));
    }

    public static void ComposeContent(IContainer container, DemandePaiementDocumentDto p)
    {
        container.Column(col =>
        {
            col.Spacing(5);
            ComposeInformationsDemande(col, p);
            ComposeObjet(col, p);
            ComposeMontant(col, p);
            ComposeBeneficiaires(col, p);
            ComposeCircuitValidation(col, p);
            ComposeTransmissionBudget(col, p);
        });
    }

    private static void ComposeHeader(IContainer container, DemandePaiementDocumentDto p)
    {
        var logo = DemandePaiementPdfAssets.Logo;
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(46).Element(e =>
                {
                    if (logo is { Length: > 0 })
                        e.Height(38).Image(logo).FitArea();
                });
                row.RelativeItem().PaddingLeft(6).AlignMiddle().Column(c =>
                {
                    c.Item().Text("SNEL").Bold().FontSize(11).FontColor(AccentBlue);
                    c.Item().Text("Société Nationale d'Électricité").SemiBold().FontSize(8).FontColor(AccentBlue);
                    c.Item().Text("e-Finance").FontSize(7.5f).FontColor(Muted);
                });
                row.ConstantItem(118).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().Text("Date d'émission").FontSize(7).FontColor(Muted);
                    c.Item().Text(p.DateEmission.ToString("dd/MM/yyyy", Fr)).Bold().FontSize(8.5f);
                    c.Item().PaddingTop(3).Text("Référence").FontSize(7).FontColor(Muted);
                    c.Item().Text(p.Reference).SemiBold().FontSize(8.5f);
                });
            });

            col.Item().PaddingTop(5).LineHorizontal(1f).LineColor(AccentLine);
            col.Item().PaddingTop(4).AlignCenter().Text("DEMANDE DE PAIEMENT").Bold().FontSize(12).FontColor(AccentBlue);
            col.Item().PaddingTop(2).AlignCenter().Text($"Référence : {p.Reference}").SemiBold().FontSize(9);
            col.Item().PaddingTop(3).AlignCenter().Text($"STATUT : {p.StatutLibelle.ToUpperInvariant()}").Bold().FontSize(8.5f);
            if (p.DocumentSignePhysiquePresent)
            {
                col.Item().PaddingTop(1).AlignCenter()
                    .Text("Document signé physique joint au dossier").FontSize(7).Italic().FontColor(Muted);
            }
            col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Border);
        });
    }

    private static void ComposeFooter(IContainer container, DemandePaiementDocumentDto p)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.6f).LineColor(AccentLine);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("Réf. ").FontSize(6.5f);
                        t.Span(p.Reference).FontSize(6.5f).SemiBold();
                    });
                    c.Item().Text("e-Finance — Société Nationale d'Électricité").FontSize(6.5f).FontColor(Muted);
                    c.Item().Text($"Document généré le {p.DateImpression.ToString("dd/MM/yyyy HH:mm", Fr)}")
                        .FontSize(6.5f).FontColor(Muted);
                });
                row.ConstantItem(120).AlignRight().Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(p.IdentifiantVerification))
                    {
                        c.Item().AlignRight().Text($"Vérification : {p.IdentifiantVerification}")
                            .FontSize(6f).FontColor(Muted);
                    }
                    c.Item().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(6.5f);
                        text.CurrentPageNumber().FontSize(6.5f);
                        text.Span(" / ").FontSize(6.5f);
                        text.TotalPages().FontSize(6.5f);
                    });
                });
            });
        });
    }

    private static void ComposeInformationsDemande(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        col.Item().Element(e => SectionTitle(e, "INFORMATIONS DE LA DEMANDE"));

        var rows = new List<(string Label, string Value)>();
        rows.Add(("Date d'émission", p.DateEmission.ToString("dd/MM/yyyy", Fr)));
        if (!string.IsNullOrWhiteSpace(p.LieuEmission))
            rows.Add(("Lieu d'émission", p.LieuEmission));
        if (!string.IsNullOrWhiteSpace(p.LibelleDemandeur))
            rows.Add(("Demandeur", p.LibelleDemandeur));
        if (!string.IsNullOrWhiteSpace(p.LibelleCasDossier))
            rows.Add(("Cas / dossier", p.LibelleCasDossier));
        if (!string.IsNullOrWhiteSpace(p.DestinationSolliciteeAffichage))
            rows.Add(("Type de budget", p.DestinationSolliciteeAffichage));
        else if (!string.IsNullOrWhiteSpace(p.TypeBudgetSollicite))
            rows.Add(("Type de budget", p.TypeBudgetSollicite));
        if (!string.IsNullOrWhiteSpace(p.ModePaiementSollicite))
            rows.Add(("Mode de paiement", LibelleModePaiement(p.ModePaiementSollicite)));
        if (!string.IsNullOrWhiteSpace(p.ItemSollicite))
            rows.Add(("Item sollicité", p.ItemSollicite));
        if (!string.IsNullOrWhiteSpace(p.CompteSection))
            rows.Add(("Compte / section", p.CompteSection));

        col.Item().Border(0.6f).BorderColor(Border).Padding(6).Column(grid =>
        {
            for (var i = 0; i < rows.Count; i += 2)
            {
                grid.Item().PaddingBottom(i + 2 < rows.Count ? 3 : 0).Row(row =>
                {
                    row.RelativeItem().Element(c => CompactInfoField(c, rows[i].Label, rows[i].Value));
                    if (i + 1 < rows.Count)
                        row.RelativeItem().Element(c => CompactInfoField(c, rows[i + 1].Label, rows[i + 1].Value));
                    else
                        row.RelativeItem();
                });
            }
        });
    }

    private static void CompactInfoField(IContainer container, string label, string value)
    {
        container.Row(r =>
        {
            r.ConstantItem(88).Text($"{label} :").FontSize(7.5f).FontColor(Muted);
            r.RelativeItem().Text(value).SemiBold().FontSize(8);
        });
    }

    private static void ComposeObjet(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        col.Item().Element(e => SectionTitle(e, "OBJET DE LA DEMANDE"));
        col.Item().Border(0.6f).BorderColor(Border).Padding(6).Text(p.Objet).FontSize(8.5f).LineHeight(1.25f);
    }

    private static void ComposeMontant(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        col.Item().Element(e => SectionTitle(e, "MONTANT DEMANDÉ"));
        col.Item().Border(0.6f).BorderColor(AccentLine).Background(Colors.Blue.Lighten5).Padding(6).AlignCenter()
            .Text($"{p.MontantBrut.ToString("N2", Fr)} {p.Devise}").Bold().FontSize(13).FontColor(AccentBlue);
    }

    private static void ComposeBeneficiaires(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        if (p.Beneficiaires.Count == 0) return;

        col.Item().Element(e => SectionTitle(e, "BÉNÉFICIAIRE(S)"));
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(18);
                c.RelativeColumn(2.2f);
                c.RelativeColumn();
                c.RelativeColumn(1.4f);
                c.RelativeColumn(1.4f);
            });

            HeaderCell(table, "N°");
            HeaderCell(table, "Nom / raison sociale");
            HeaderCell(table, "Type");
            HeaderCell(table, "Identité / fonction");
            HeaderCell(table, "Banque / compte");

            var idx = 1;
            foreach (var b in p.Beneficiaires.OrderBy(x => x.Ordre))
            {
                var nom = !string.IsNullOrWhiteSpace(b.NomComplet)
                    ? b.NomComplet
                    : b.RaisonSociale ?? "—";
                var type = b.TypeBeneficiaire ?? "—";
                var identite = JoinNonEmpty(
                    b.EstPrincipal ? "Principal" : null,
                    b.Matricule is not null ? $"Mat. {b.Matricule}" : null,
                    b.Fonction,
                    b.Rccm is not null ? $"RCCM {b.Rccm}" : null);
                var banque = JoinNonEmpty(b.Banque, b.NumeroCompte is not null ? $"Cpt {b.NumeroCompte}" : null);

                table.Cell().Element(CompactBodyCell).Text(idx.ToString(Fr));
                table.Cell().Element(CompactBodyCell).Text(nom).SemiBold();
                table.Cell().Element(CompactBodyCell).Text(type);
                table.Cell().Element(CompactBodyCell).Text(string.IsNullOrWhiteSpace(identite) ? "—" : identite);
                table.Cell().Element(CompactBodyCell).Text(string.IsNullOrWhiteSpace(banque) ? "—" : banque);
                idx++;
            }
        });
    }

    private static void ComposeCircuitValidation(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        col.Item().PaddingTop(2).Element(e => SectionTitle(e, "CIRCUIT DE VALIDATION DE L'ENTITÉ"));
        col.Item().Row(row =>
        {
            row.Spacing(6);
            var n1 = p.ValidationsEntite.FirstOrDefault(x => x.Niveau == ValidationEntiteNiveau.N1);
            var n2 = p.ValidationsEntite.FirstOrDefault(x => x.Niveau == ValidationEntiteNiveau.N2);
            row.RelativeItem().Element(e => ComposeNiveauValidation(e, ValidationEntiteNiveau.N1, n1));
            row.RelativeItem().Element(e => ComposeNiveauValidation(e, ValidationEntiteNiveau.N2, n2));
        });
    }

    private static void ComposeNiveauValidation(IContainer container, byte niveau, ValidationEntiteDto? v)
    {
        var titre = niveau switch
        {
            ValidationEntiteNiveau.N1 => "RESPONSABLE NIVEAU 1",
            ValidationEntiteNiveau.N2 => "RESPONSABLE NIVEAU 2",
            _ => $"NIVEAU {niveau}",
        };

        var statut = v?.Statut ?? StatutValidationEntite.EnAttente;
        var validee = statut == StatutValidationEntite.Validee && v is not null;
        var mode = validee ? ModeValidationEntite.Normaliser(v!.ModeValidation) : null;

        container.Border(0.6f).BorderColor(Border).Padding(6).MinHeight(118).Column(col =>
        {
            col.Item().Text(titre).Bold().FontSize(8).FontColor(AccentBlue);

            if (validee && mode == ModeValidationEntite.Electronique)
                ComposeValidationElectronique(col, v!);
            else if (validee && mode == ModeValidationEntite.Physique)
                ComposeValidationPhysique(col, v!);
            else
                ComposeZonesSignaturePhysique(col);
        });
    }

    private static void ComposeValidationElectronique(ColumnDescriptor col, ValidationEntiteDto v)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Text("☑ Électronique").FontSize(7.5f).FontColor(Colors.Green.Darken3);
            c.Item().Text("☐ Physique").FontSize(7.5f).FontColor(Muted);
            c.Item().PaddingTop(3).Background(Colors.Green.Lighten5).Padding(4).Column(inner =>
            {
                inner.Item().Text("Validation électronique").Bold().FontSize(7.5f);
                inner.Item().Text($"Validé par : {v.NomUtilisateurValidateur ?? "—"}").FontSize(7.5f);
                if (v.DateValidation is not null)
                    inner.Item().Text($"Date : {v.DateValidation.Value.ToString("dd/MM/yyyy HH:mm", Fr)}").FontSize(7.5f);
                if (!string.IsNullOrWhiteSpace(v.Commentaire))
                    inner.Item().Text(v.Commentaire).FontSize(7f).FontColor(Muted);
            });
        });
    }

    private static void ComposeValidationPhysique(ColumnDescriptor col, ValidationEntiteDto v)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Text("☐ Électronique").FontSize(7.5f).FontColor(Muted);
            c.Item().Text("☑ Physique").FontSize(7.5f).FontColor(Colors.Orange.Darken3);
            c.Item().PaddingTop(3).Background(Colors.Orange.Lighten5).Padding(4).Column(inner =>
            {
                inner.Item().Text("Validation physique").Bold().FontSize(7.5f);
                inner.Item().Text($"Signataire : {v.NomSignatairePhysique ?? "—"}").FontSize(7.5f).SemiBold();
                if (!string.IsNullOrWhiteSpace(v.FonctionSignatairePhysique))
                    inner.Item().Text($"Fonction : {v.FonctionSignatairePhysique}").FontSize(7.5f);
                if (v.DateSignaturePhysique is not null)
                    inner.Item().Text($"Date : {v.DateSignaturePhysique.Value.ToString("dd/MM/yyyy", Fr)}").FontSize(7.5f);
            });
            if (!string.IsNullOrWhiteSpace(v.NomUtilisateurDeclarant))
            {
                c.Item().PaddingTop(2).Text($"Déclaré par : {v.NomUtilisateurDeclarant}").FontSize(6.5f).FontColor(Muted);
                if (v.DateValidation is not null)
                    c.Item().Text($"({v.DateValidation.Value.ToString("dd/MM/yyyy HH:mm", Fr)})").FontSize(6.5f).FontColor(Muted);
            }
        });
    }

    private static void ComposeZonesSignaturePhysique(ColumnDescriptor col)
    {
        col.Item().PaddingTop(4).Column(c =>
        {
            c.Item().Text("Nom : _________________________________").FontSize(7.5f);
            c.Item().PaddingTop(14).MinHeight(28).Text("Signature :").FontSize(7.5f);
            c.Item().PaddingTop(8).Text("Date : _________________________________").FontSize(7.5f);
            c.Item().PaddingTop(6).Row(r =>
            {
                r.RelativeItem().Text("☐ Électronique").FontSize(7f).FontColor(Muted);
                r.RelativeItem().Text("☐ Physique").FontSize(7f).FontColor(Muted);
            });
        });
    }

    private static void ComposeTransmissionBudget(ColumnDescriptor col, DemandePaiementDocumentDto p)
    {
        var transmise = p.DateSoumission is not null
            || string.Equals(p.Statut, StatutDemandePaiement.Soumise, StringComparison.Ordinal)
            || EstApresSoumission(p.Statut);

        col.Item().PaddingTop(2).Element(e => SectionTitle(e, "TRANSMISSION AU BUDGET"));
        col.Item().Border(0.6f).BorderColor(Border).Padding(5).Column(c =>
        {
            if (transmise)
            {
                c.Item().Text("☑ TRANSMISE AU BUDGET").Bold().FontSize(8).FontColor(AccentBlue);
                if (p.DateSoumission is not null)
                    c.Item().Text($"Date : {p.DateSoumission.Value.ToString("dd/MM/yyyy HH:mm", Fr)}").FontSize(7.5f);
            }
            else
            {
                c.Item().Text("☐ À transmettre au Budget").FontSize(8).FontColor(Muted);
            }
        });
    }

    private static bool EstApresSoumission(string statut)
        => statut is StatutDemandePaiement.Soumise
            or StatutDemandePaiement.EnTraitementDpm
            or StatutDemandePaiement.EnControleBudgetaire
            or StatutDemandePaiement.ViseeBudgetairement;

    private static void SectionTitle(IContainer container, string title)
    {
        container.PaddingBottom(2).Text(title).Bold().FontSize(8.5f).FontColor(AccentBlue);
    }

    private static IContainer CompactBodyCell(IContainer c)
        => c.BorderBottom(0.3f).BorderColor(Border).PaddingVertical(2).PaddingHorizontal(4);

    private static void HeaderCell(TableDescriptor header, string text)
        => header.Cell().Background(AccentBlue).Padding(3).Text(text).FontColor(Colors.White).SemiBold().FontSize(7);

    private static string LibelleModePaiement(string mode)
        => ModePaiementDpm.Normaliser(mode) switch
        {
            ModePaiementDpm.Caisse => "CAISSE",
            ModePaiementDpm.Banque => "BANQUE",
            _ => mode,
        };

    private static string JoinNonEmpty(params string?[] parts)
        => string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
