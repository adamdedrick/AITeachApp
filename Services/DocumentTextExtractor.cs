using NPOI.HWPF.Extractor;
using NPOI.HWPF;
using NPOI.XWPF.UserModel;
using UglyToad.PdfPig;

namespace AITeach1.Services;

internal static class DocumentTextExtractor
{
    public static string Extract(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".txt" => File.ReadAllText(filePath),
            ".pdf" => ExtractPdf(filePath),
            ".doc" => ExtractWordDocument(filePath),
            ".docx" => ExtractWordOpenXmlDocument(filePath),
            _ => throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(filePath)}")
        };
    }

    private static string ExtractPdf(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static string ExtractWordDocument(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var document = new HWPFDocument(stream);
        var extractor = new WordExtractor(document);
        return extractor.Text;
    }

    private static string ExtractWordOpenXmlDocument(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var document = new XWPFDocument(stream);

        var paragraphs = document.Paragraphs
            .Select(paragraph => paragraph.ParagraphText);
        var tableRows = document.Tables
            .SelectMany(table => table.Rows)
            .Select(row => string.Join(" | ", row.GetTableCells().Select(cell =>
                string.Join(" ", cell.Paragraphs.Select(paragraph => paragraph.ParagraphText)))));

        return string.Join(Environment.NewLine, paragraphs.Concat(tableRows)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    public static IEnumerable<string> SplitIntoChunks(string text, int chunkSize = 1100, int overlap = 140)
    {
        text = text.Trim();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            if (end < text.Length)
            {
                var boundary = text.LastIndexOfAny([' ', '\n', '\r', '\t'], end - 1, end - start);
                if (boundary > start + chunkSize * 2 / 3)
                {
                    end = boundary;
                }
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0)
            {
                yield return chunk;
            }

            if (end >= text.Length)
            {
                break;
            }

            start = Math.Max(start + 1, end - overlap);
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }
    }
}
