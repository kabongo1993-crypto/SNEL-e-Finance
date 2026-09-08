using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BudgetWeb.Application.DTOs;
using BudgetWeb.Infrastructure.Documents;

var renderer = new QuestPdfBilletConversionRenderer();
var dto = new BilletConversionDocumentDto(
    26, "0298", "IGWE", 870_000m, "USD", 453m,
    new DateOnly(2026, 6, 7), null, null, 394_110_000m, null,
    "Charge DPM Test", null, null, new DateTime(2026, 8, 28));
var pdf = renderer.Render(dto);
var dir = @"docs\test-output";
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "billet-conversion-v2-sample.pdf");
File.WriteAllBytes(path, pdf);
var raw = Encoding.Latin1.GetString(pdf);
var pages = Regex.Matches(raw, @"/Type\s*/Page\b");
Console.WriteLine($"Bytes: {pdf.Length}");
Console.WriteLine($"/Type /Page matches: {pages.Count}");
var media = Regex.Match(raw, @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
if (media.Success) Console.WriteLine($"MediaBox: {media.Groups[1].Value} x {media.Groups[2].Value} pt");
Console.WriteLine($"Saved: {Path.GetFullPath(path)}");
