using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StructuralLoadAssistant.Reporting;

/// <summary>
/// Standalone, pure C# minimal PDF document generator (PDF 1.4 compliant).
/// Outputs deterministically formatted vector PDF reports without external dependencies or printer drivers.
/// Handles multi-page flowing sections and automatic line wrapping so no trace nodes or data rows are clipped.
/// </summary>
public sealed class SimplePdfWriter
{
    private readonly List<string> pages = new();
    public string Title { get; set; } = "Engineering Report";
    public string Watermark { get; set; } = "ENGINEER REVIEW - NOT FOR CONSTRUCTION";

    public void AddPage(string pageText)
    {
        pages.Add(pageText);
    }

    public void AddFlowingSection(string sectionText)
    {
        var rawLines = sectionText.Split('\n');
        var pageLines = new List<string>();
        double currentY = 785;

        void AddLine(string text)
        {
            if (currentY < 60)
            {
                if (pageLines.Count > 0)
                {
                    pages.Add(string.Join("\n", pageLines));
                    pageLines.Clear();
                }
                currentY = 785;
            }
            pageLines.Add(text);
            currentY -= 13;
        }

        foreach (var rawLine in rawLines)
        {
            string clean = rawLine.TrimEnd('\r');
            // Courier is monospaced at 0.6 * fontSize per character. Headings are
            // rendered larger than body text (12pt / 10pt vs 9pt), so they must
            // wrap earlier or they run off the right edge of the 595pt page
            // (40pt margins leave 515pt of usable width).
            int maxChars = clean.StartsWith("# ") ? 70 : clean.StartsWith("## ") ? 84 : 92;
            if (clean.Length <= maxChars)
            {
                AddLine(clean);
            }
            else
            {
                // Wrap gracefully so wide tables and long formulas fit without right-edge clipping
                string rem = clean;
                while (rem.Length > maxChars)
                {
                    int cut = rem.LastIndexOf(' ', maxChars);
                    if (cut <= 15) cut = maxChars;
                    AddLine(rem.Substring(0, cut));
                    rem = "  " + rem.Substring(cut).TrimStart();
                }
                if (!string.IsNullOrEmpty(rem))
                {
                    AddLine(rem);
                }
            }
        }

        if (pageLines.Count > 0)
        {
            pages.Add(string.Join("\n", pageLines));
        }
    }

    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);
        var offsets = new List<long>();

        void WriteLine(string s) { writer.Write(s + "\n"); writer.Flush(); }

        WriteLine("%PDF-1.4");
        // Binary marker comment (tells readers the file contains binary data).
        // It must be written as raw bytes: through the ASCII StreamWriter the
        // high bytes were previously emitted as literal '?' characters.
        writer.Flush();
        ms.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6);

        // Object 1: Catalog
        offsets.Add(ms.Position);
        WriteLine("1 0 obj");
        WriteLine("<< /Type /Catalog /Pages 2 0 R >>");
        WriteLine("endobj");

        // Object 2: Pages
        offsets.Add(ms.Position);
        WriteLine("2 0 obj");
        var kids = new StringBuilder();
        for (int i = 0; i < pages.Count; i++) kids.Append($"{4 + i * 2} 0 R ");
        WriteLine($"<< /Type /Pages /Kids [{kids.ToString().Trim()}] /Count {pages.Count} >>");
        WriteLine("endobj");

        // Object 3: Font (Courier standard 14 font)
        offsets.Add(ms.Position);
        WriteLine("3 0 obj");
        WriteLine("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        WriteLine("endobj");

        for (int i = 0; i < pages.Count; i++)
        {
            int pageObjNum = 4 + i * 2;
            int contentObjNum = pageObjNum + 1;

            // Page object
            offsets.Add(ms.Position);
            WriteLine($"{pageObjNum} 0 obj");
            WriteLine($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentObjNum} 0 R /Resources << /Font << /F1 3 0 R >> >> >>");
            WriteLine("endobj");

            // Content stream
            var content = new StringBuilder();
            content.Append("BT\n");
            content.Append("/F1 10 Tf\n");

            // Watermark on every page
            content.Append("1 0 0 rg\n"); // red tint for review watermark
            content.Append("40 810 Td\n");
            content.Append($"({EscapePdf(Watermark)}) Tj\n");
            content.Append("0 0 0 rg\n"); // reset black

            // Page Number
            content.Append("430 0 Td\n");
            content.Append($"({EscapePdf($"Page {i + 1} of {pages.Count}")}) Tj\n");
            content.Append("-430 -25 Td\n");

            // Body lines
            var lines = pages[i].Split('\n');
            double currentY = 785;
            foreach (var line in lines)
            {
                string clean = line.TrimEnd('\r');
                if (clean.StartsWith("# "))
                {
                    content.Append("/F1 12 Tf\n");
                    content.Append($"({EscapePdf(clean.Substring(2))}) Tj\n");
                    content.Append("/F1 9 Tf\n");
                }
                else if (clean.StartsWith("## "))
                {
                    content.Append("/F1 10 Tf\n");
                    content.Append($"({EscapePdf(clean.Substring(3))}) Tj\n");
                    content.Append("/F1 9 Tf\n");
                }
                else
                {
                    content.Append($"({EscapePdf(clean)}) Tj\n");
                }
                content.Append("0 -13 Td\n");
                currentY -= 13;
            }
            content.Append("ET\n");

            byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());
            offsets.Add(ms.Position);
            WriteLine($"{contentObjNum} 0 obj");
            WriteLine($"<< /Length {contentBytes.Length} >>");
            WriteLine("stream");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            WriteLine("\nendstream");
            WriteLine("endobj");
        }

        // Info object: carries the document Title that Build() callers set.
        int infoObjNum = offsets.Count + 1;
        offsets.Add(ms.Position);
        WriteLine($"{infoObjNum} 0 obj");
        WriteLine($"<< /Title ({EscapePdf(Title)}) /Subject ({EscapePdf(Watermark)}) >>");
        WriteLine("endobj");

        long startXref = ms.Position;
        WriteLine("xref");
        WriteLine($"0 {offsets.Count + 1}");
        WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            WriteLine($"{offset:D10} 00000 n ");
        }

        WriteLine("trailer");
        WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R /Info {infoObjNum} 0 R >>");
        WriteLine("startxref");
        WriteLine($"{startXref}");
        WriteLine("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string s)
    {
        // The Type1 Courier font used here has no Unicode encoding, so text is
        // transliterated to ASCII instead of being silently blanked:
        // - accents/diacritics are stripped via canonical decomposition
        //   (e.g. Vietnamese and other Latin-script names stay readable),
        // - known symbols and punctuation get an explicit ASCII rendering,
        // - only characters with no reasonable rendering become a space.
        var sb = new StringBuilder();
        string decomposed = s.Normalize(NormalizationForm.FormD);
        foreach (char c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue; // diacritic of the preceding base letter: drop it, keep the letter
            }
            if (c is '(' or ')' or '\\') sb.Append('\\').Append(c);
            else if (c == '\t') sb.Append("  ");
            else if (c >= 32 && c <= 126) sb.Append(c);
            else if (c == 'đ') sb.Append('d');
            else if (c == 'Đ') sb.Append('D');
            else if (c == 'θ' || c == 'Θ') sb.Append("theta");
            else if (c == 'Δ') sb.Append("Delta");
            else if (c == 'α') sb.Append("alpha");
            else if (c == 'β') sb.Append("beta");
            else if (c == 'λ') sb.Append("lambda");
            else if (c == 'μ') sb.Append("mu");
            else if (c == 'π') sb.Append("pi");
            else if (c == 'ρ') sb.Append("rho");
            else if (c == 'φ') sb.Append("phi");
            else if (c == '≤') sb.Append("<=");
            else if (c == '≥') sb.Append(">=");
            else if (c == '±') sb.Append("+/-");
            else if (c == '×') sb.Append("x");
            else if (c == '°') sb.Append(" deg");
            else if (c == '²') sb.Append("^2");
            else if (c == '³') sb.Append("^3");
            else if (c == '–' || c == '—') sb.Append('-');
            else if (c == '‘' || c == '’') sb.Append('\'');
            else if (c == '“' || c == '”') sb.Append('"');
            else if (c == '→') sb.Append("->");
            else if (c == '←') sb.Append("<-");
            else if (c == '•') sb.Append('-');
            else if (c == '…') sb.Append("...");
            else if (c == '✓') sb.Append("OK");
            else sb.Append(' ');
        }
        return sb.ToString();
    }
}
