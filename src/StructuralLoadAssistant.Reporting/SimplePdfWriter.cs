using System.Collections.Generic;
using System.IO;
using System.Text;
namespace StructuralLoadAssistant.Reporting;
/// <summary>
/// Standalone, pure C# minimal PDF document generator (PDF 1.4 compliant).
/// Outputs deterministically formatted vector PDF reports without external dependencies or printer drivers.
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
    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);
        var offsets = new List<long>();

        //
        void WriteLine(string s) { writer.Write(s + "\n"); writer.Flush(); }

        WriteLine("%PDF-1.4");
        WriteLine("%\xE2\xE3\xCF\xD3");

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
            content.Append("450 0 Td\n");
            content.Append($"({EscapePdf($"Page {i + 1} of {pages.Count}")}) Tj\n");
            content.Append("-450 -25 Td\n");

            // Body lines
            var lines = pages[i].Split('\n');
            double currentY = 785;
            foreach (var line in lines)
            {
                if (currentY < 45) break;
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

        long startXref = ms.Position;
        WriteLine("xref");
        WriteLine($"0 {offsets.Count + 1}");
        WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets)
        {
            WriteLine($"{offset:D10} 00000 n ");
        }

        WriteLine("trailer");
        WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        WriteLine("startxref");
        WriteLine($"{startXref}");
        WriteLine("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c is '(' or ')' or '\\') sb.Append('\\').Append(c);
            else if (c >= 32 && c <= 126) sb.Append(c);
            else sb.Append(' ');
        }
        return sb.ToString();
    }
}