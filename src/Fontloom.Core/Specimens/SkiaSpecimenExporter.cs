using Fontloom.Core.Fonts;
using SkiaSharp;

namespace Fontloom.Core.Specimens;

public sealed class SkiaSpecimenExporter : ISpecimenExporter
{
    private const int PngWidth = 1800;
    private const int PngHeight = 1200;

    public void ExportFontPng(FontInfo font, string outputPath, SpecimenExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(font);
        var normalizedOutputPath = ValidateOutputPath(outputPath);
        var normalizedOptions = NormalizeOptions(options);

        EnsureDirectory(normalizedOutputPath);

        using var surface = SKSurface.Create(new SKImageInfo(PngWidth, PngHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        DrawSpecimenPage(
            canvas,
            font,
            pageTitle: $"Specimen — {font.Family}",
            sampleText: normalizedOptions.SampleText,
            samplePointSize: Math.Clamp(normalizedOptions.PointSize, 12, 160),
            pageWidth: PngWidth,
            pageHeight: PngHeight,
            pageNumber: null,
            totalPages: null);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        using var fileStream = File.Open(normalizedOutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(fileStream);
    }

    public void ExportCollectionPdf(IReadOnlyList<FontInfo> fonts, string outputPath, SpecimenExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        if (fonts.Count == 0)
        {
            throw new ArgumentException("At least one font is required to export a collection specimen.", nameof(fonts));
        }

        var normalizedOutputPath = ValidateOutputPath(outputPath);
        var normalizedOptions = NormalizeOptions(options);

        EnsureDirectory(normalizedOutputPath);

        const float pageWidth = 595f;   // A4 width in points
        const float pageHeight = 842f;  // A4 height in points

        using var document = SKDocument.CreatePdf(normalizedOutputPath);
        for (var pageIndex = 0; pageIndex < fonts.Count; pageIndex++)
        {
            var font = fonts[pageIndex];
            using var canvas = document.BeginPage(pageWidth, pageHeight);

            DrawSpecimenPage(
                canvas,
                font,
                pageTitle: normalizedOptions.CollectionLabel is { Length: > 0 }
                    ? $"Collection specimen — {normalizedOptions.CollectionLabel}"
                    : "Collection specimen",
                sampleText: normalizedOptions.SampleText,
                samplePointSize: Math.Clamp(normalizedOptions.PointSize, 10, 48),
                pageWidth: pageWidth,
                pageHeight: pageHeight,
                pageNumber: pageIndex + 1,
                totalPages: fonts.Count);

            document.EndPage();
        }

        document.Close();
    }

    private static void DrawSpecimenPage(
        SKCanvas canvas,
        FontInfo font,
        string pageTitle,
        string sampleText,
        float samplePointSize,
        float pageWidth,
        float pageHeight,
        int? pageNumber,
        int? totalPages)
    {
        const float left = 48f;
        float cursorY = 72f;

        using var titlePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#1E1E1E"),
            TextSize = 30f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        using var headingPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#111111"),
            TextSize = 40f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        using var metaPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#505050"),
            TextSize = 18f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        DrawTextLine(canvas, pageTitle, left, ref cursorY, titlePaint, 36f);
        DrawTextLine(canvas, $"{font.Family} — {font.Subfamily}", left, ref cursorY, headingPaint, 48f);

        var styleToken = font.IsItalic ? "Italic" : "Upright";
        DrawTextLine(canvas, $"Weight {font.Weight} · Width {font.Width} · {styleToken}", left, ref cursorY, metaPaint, 26f);
        DrawTextLine(canvas, $"Format {font.Format} · Glyphs {font.Coverage.GlyphCount:N0} · Mapped {font.Coverage.MappedCodePointCount:N0}", left, ref cursorY, metaPaint, 26f);

        if (pageNumber.HasValue && totalPages.HasValue)
        {
            DrawTextLine(canvas, $"Page {pageNumber.Value} of {totalPages.Value}", left, ref cursorY, metaPaint, 28f);
        }

        cursorY += 8f;

        using var sampleBorderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = SKColor.Parse("#D2D2D2")
        };

        var sampleRect = new SKRect(left, cursorY, pageWidth - left, pageHeight - 64f);
        canvas.DrawRect(sampleRect, sampleBorderPaint);

        using var samplePaint = BuildSamplePaint(font, samplePointSize);

        var textStartX = sampleRect.Left + 18f;
        var textStartY = sampleRect.Top + samplePaint.TextSize + 22f;
        var maxTextWidth = sampleRect.Width - 36f;

        DrawWrappedText(canvas, sampleText, samplePaint, textStartX, textStartY, maxTextWidth, lineSpacing: 10f);

        using var sourcePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#707070"),
            TextSize = 12f,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        var sourceText = $"Source: {font.SourcePath}";
        DrawWrappedText(canvas, sourceText, sourcePaint, left, pageHeight - 30f, pageWidth - (left * 2), lineSpacing: 2f);
    }

    private static SKPaint BuildSamplePaint(FontInfo font, float samplePointSize)
    {
        var slant = font.IsItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var skWeight = font.Weight switch
        {
            <= 150 => SKFontStyleWeight.Thin,
            <= 250 => SKFontStyleWeight.ExtraLight,
            <= 350 => SKFontStyleWeight.Light,
            <= 450 => SKFontStyleWeight.Normal,
            <= 550 => SKFontStyleWeight.Medium,
            <= 650 => SKFontStyleWeight.SemiBold,
            <= 750 => SKFontStyleWeight.Bold,
            <= 850 => SKFontStyleWeight.ExtraBold,
            _ => SKFontStyleWeight.Black
        };

        SKTypeface? typeface = null;
        try
        {
            typeface = SKTypeface.FromFile(font.SourcePath, font.FaceIndex);
        }
        catch
        {
            // Fallback to system-resolved family style if file loading fails.
            typeface = SKTypeface.FromFamilyName(font.Family, skWeight, SKFontStyleWidth.Normal, slant);
        }

        if (typeface is null)
        {
            typeface = SKTypeface.Default;
        }

        return new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("#111111"),
            TextSize = samplePointSize,
            Typeface = typeface
        };
    }

    private static void DrawWrappedText(
        SKCanvas canvas,
        string text,
        SKPaint paint,
        float x,
        float y,
        float maxWidth,
        float lineSpacing)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return;
        }

        var currentLine = string.Empty;
        var cursorY = y;

        foreach (var word in words)
        {
            var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
            var candidateWidth = paint.MeasureText(candidate);

            if (candidateWidth <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (currentLine.Length > 0)
            {
                canvas.DrawText(currentLine, x, cursorY, paint);
                cursorY += paint.TextSize + lineSpacing;
            }

            currentLine = word;
        }

        if (currentLine.Length > 0)
        {
            canvas.DrawText(currentLine, x, cursorY, paint);
        }
    }

    private static void DrawTextLine(SKCanvas canvas, string text, float x, ref float y, SKPaint paint, float lineAdvance)
    {
        canvas.DrawText(text, x, y, paint);
        y += lineAdvance;
    }

    private static void EnsureDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
        }

        return Path.GetFullPath(outputPath);
    }

    private static SpecimenExportOptions NormalizeOptions(SpecimenExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sampleText = string.IsNullOrWhiteSpace(options.SampleText)
            ? SpecimenExportOptions.Default.SampleText
            : options.SampleText.Trim();

        var pointSize = options.PointSize <= 0 ? SpecimenExportOptions.Default.PointSize : options.PointSize;

        return options with
        {
            SampleText = sampleText,
            PointSize = pointSize
        };
    }
}
