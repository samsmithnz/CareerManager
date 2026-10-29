using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CareerManager.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace CareerManager.Services;

public static class DocumentExportService
{
    private static readonly object FontResolverLock = new();
    private static readonly Lazy<byte[]> FontData = new(LoadFontData);
    private static bool _fontResolverRegistered;

    public static byte[] GeneratePdf(CareerDocument document)
    {
        EnsureFontResolver();
        using var output = new MemoryStream();
        using var pdf = new PdfDocument();
        pdf.Info.Title = document.Title;
        var pdfFontOptions = new XPdfFontOptions(PdfFontEncoding.Unicode);
        var titleFont = new XFont("DejaVu Sans", 18, XFontStyleEx.Bold, pdfFontOptions);
        var bodyFont = new XFont("DejaVu Sans", 11, XFontStyleEx.Regular, pdfFontOptions);
        const double pageWidth = 612;
        const double pageHeight = 792;
        const double margin = 54;
        const double bodyLineHeight = 15;
        var y = margin;
        XGraphics? graphics = null;

        void AddPage()
        {
            graphics?.Dispose();
            var page = pdf.AddPage();
            page.Size = PdfSharp.PageSize.Letter;
            graphics = XGraphics.FromPdfPage(page);
            y = margin;
        }

        void DrawParagraph(string text, XFont font, double lineHeight, double paragraphSpacing)
        {
            if (string.IsNullOrEmpty(text))
            {
                y += lineHeight;
                return;
            }

            var lines = WrapText(text, font, graphics!, pageWidth - (2 * margin));
            foreach (var line in lines)
            {
                if (y + lineHeight > pageHeight - margin)
                {
                    AddPage();
                }

                graphics!.DrawString(line, font, XBrushes.Black, new XPoint(margin, y + lineHeight));
                y += lineHeight;
            }

            y += paragraphSpacing;
        }

        try
        {
            AddPage();
            DrawParagraph(document.Title, titleFont, 24, 10);
            foreach (var paragraph in NormalizeLines(document.Text))
            {
                DrawParagraph(paragraph, bodyFont, bodyLineHeight, 0);
            }
        }
        finally
        {
            graphics?.Dispose();
        }

        pdf.Save(output, closeStream: false);
        return output.ToArray();
    }

    public static byte[] GenerateDocx(CareerDocument document)
    {
        using var output = new MemoryStream();
        using (var package = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document))
        {
            var mainPart = package.AddMainDocumentPart();
            var body = new Word.Body();
            var title = new Word.Paragraph(
                CreateRun(document.Title, 36, bold: true));
            body.Append(title);

            foreach (var line in NormalizeLines(document.Text))
            {
                var paragraph = new Word.Paragraph(
                    new Word.ParagraphProperties(
                        new Word.SpacingBetweenLines { After = "120", Line = "276", LineRule = Word.LineSpacingRuleValues.Auto }),
                    CreateRun(line, 22));
                body.Append(paragraph);
            }

            body.Append(new Word.SectionProperties(
                new Word.PageSize { Width = 12240, Height = 15840 },
                new Word.PageMargin
                {
                    Top = 1440,
                    Right = 1440,
                    Bottom = 1440,
                    Left = 1440,
                    Header = 720,
                    Footer = 720,
                    Gutter = 0
                }));
            mainPart.Document = new Word.Document(body);
            mainPart.Document.Save();
        }

        return output.ToArray();
    }

    private static Word.Run CreateRun(string text, int fontSize, bool bold = false)
    {
        var properties = new Word.RunProperties(
            new Word.RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
            new Word.FontSize { Val = fontSize.ToString() });
        if (bold)
        {
            properties.Append(new Word.Bold());
        }

        return new Word.Run(properties, new Word.Text(text)
        {
            Space = SpaceProcessingModeValues.Preserve
        });
    }

    private static IEnumerable<string> NormalizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static IReadOnlyList<string> WrapText(string text, XFont font, XGraphics graphics, double width)
    {
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (Match match in Regex.Matches(text, @"\s+|\S+"))
        {
            var token = match.Value.Replace("\t", "    ", StringComparison.Ordinal);
            if (char.IsWhiteSpace(token[0]))
            {
                if (current.Length > 0 && Measure(graphics, current + token, font) > width)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                AppendCharacters(token);
                continue;
            }

            if (Measure(graphics, current + token, font) <= width)
            {
                current.Append(token);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            AppendCharacters(token);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;

        void AppendCharacters(string value)
        {
            foreach (var rune in value.EnumerateRunes())
            {
                var character = rune.ToString();
                if (current.Length > 0 && Measure(graphics, current + character, font) > width)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                current.Append(character);
            }
        }
    }

    private static double Measure(XGraphics graphics, string text, XFont font) =>
        graphics.MeasureString(text, font).Width;

    private static void EnsureFontResolver()
    {
        lock (FontResolverLock)
        {
            if (_fontResolverRegistered)
            {
                return;
            }

            GlobalFontSettings.FontResolver ??= new StandardFontResolver();
            _fontResolverRegistered = true;
        }
    }

    private sealed class StandardFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo("DejaVuSans", isBold, isItalic);
        }

        public byte[] GetFont(string faceName) => FontData.Value;
    }

    private static byte[] LoadFontData()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CareerManager.Web.Fonts.DejaVuSans.ttf")
            ?? throw new InvalidOperationException("The bundled PDF font could not be loaded.");
        using var font = new MemoryStream();
        stream.CopyTo(font);
        return font.ToArray();
    }
}
