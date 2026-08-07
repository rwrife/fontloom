using System.IO.Compression;
using OpenFontSharp;
using OpenFontSharp.Tables;
using OpenFontSharp.WebFont;

namespace Fontloom.Core.Fonts;

public sealed class OpenTypeFontFileReader : IFontFileReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc",
        ".woff",
        ".woff2"
    };

    private static readonly object WebFontHandlerLock = new();
    private static bool _webFontHandlersConfigured;

    public IReadOnlyList<FontInfo> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Font path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FontReadException(
                FontReadErrorCode.FileNotFound,
                path,
                $"Font file not found: {path}");
        }

        var extension = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new FontReadException(
                FontReadErrorCode.UnsupportedFormat,
                path,
                $"Unsupported font format: '{extension}'.");
        }

        byte[] rawBytes;
        try
        {
            rawBytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            throw new FontReadException(
                FontReadErrorCode.IoError,
                path,
                $"Failed to read font file '{path}'.",
                ex);
        }

        try
        {
            EnsureWebFontDecompressHandlers();

            var openFontReader = new OpenFontReader();
            PreviewFontInfo preview;

            using (var previewStream = new MemoryStream(rawBytes, writable: false))
            {
                preview = openFontReader.ReadPreview(previewStream);
            }

            var format = ResolveFormat(extension, preview.IsFontCollection);

            if (preview.IsFontCollection)
            {
                return ReadCollectionFaces(rawBytes, path, openFontReader, preview, format);
            }

            var typeface = ReadTypeface(rawBytes, openFontReader, preview.ActualStreamOffset);
            return [CreateFontInfo(path, 0, preview, typeface, format)];
        }
        catch (FontReadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FontReadException(
                FontReadErrorCode.CorruptOrUnsupportedFont,
                path,
                $"Failed to parse font '{path}'.",
                ex);
        }
    }

    private static IReadOnlyList<FontInfo> ReadCollectionFaces(
        byte[] rawBytes,
        string path,
        OpenFontReader reader,
        PreviewFontInfo collectionPreview,
        FontContainerFormat format)
    {
        var memberCount = collectionPreview.MemberCount;
        if (memberCount <= 0)
        {
            throw new FontReadException(
                FontReadErrorCode.CorruptOrUnsupportedFont,
                path,
                "Font collection reported zero members.");
        }

        var faces = new List<FontInfo>(memberCount);
        for (var index = 0; index < memberCount; index++)
        {
            var memberPreview = collectionPreview.GetMember(index);
            var typeface = ReadTypeface(rawBytes, reader, memberPreview.ActualStreamOffset);
            faces.Add(CreateFontInfo(path, index, memberPreview, typeface, format));
        }

        return faces;
    }

    private static Typeface ReadTypeface(byte[] rawBytes, OpenFontReader reader, int streamOffset)
    {
        using var stream = new MemoryStream(rawBytes, writable: false);
        return reader.Read(stream, streamOffset, ReadFlags.Full);
    }

    private static FontInfo CreateFontInfo(
        string sourcePath,
        int faceIndex,
        PreviewFontInfo preview,
        Typeface typeface,
        FontContainerFormat format)
    {
        var family = FirstNonBlank(preview.Name, typeface.Name, "Unknown");
        var subfamily = FirstNonBlank(preview.SubFamilyName, typeface.FontSubFamily, "Regular");

        var os2 = typeface.OS2Table;
        var weight = ResolveWeight(os2, preview);
        var width = ResolveWidth(os2, preview);

        return new FontInfo(
            SourcePath: sourcePath,
            FaceIndex: faceIndex,
            Family: family,
            Subfamily: subfamily,
            Weight: weight,
            Width: width,
            IsItalic: IsItalic(preview, subfamily),
            Format: format,
            Coverage: BuildCoverageSummary(typeface));
    }

    private static string FirstNonBlank(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "Unknown";
    }

    private static int ResolveWeight(OS2Table? os2, PreviewFontInfo preview)
    {
        var os2Weight = (int)(os2?.usWeightClass ?? 0);
        if (os2Weight > 0)
        {
            return os2Weight;
        }

        var previewWeight = (int)preview.WeightClass;
        return previewWeight > 0 ? previewWeight : 400;
    }

    private static FontWidthClass ResolveWidth(OS2Table? os2, PreviewFontInfo preview)
    {
        var os2Width = (ushort)(os2?.usWidthClass ?? 0);
        if (os2Width > 0)
        {
            return MapWidthClass(os2Width);
        }

        var previewWidth = (ushort)preview.WidthClass;
        return MapWidthClass(previewWidth);
    }

    private static bool IsItalic(PreviewFontInfo preview, string subfamily)
    {
        var translated = preview.OS2TranslatedStyle.ToString();
        if (translated.Contains("ITALIC", StringComparison.OrdinalIgnoreCase) ||
            translated.Contains("OBLIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return subfamily.Contains("italic", StringComparison.OrdinalIgnoreCase) ||
               subfamily.Contains("oblique", StringComparison.OrdinalIgnoreCase);
    }

    private static GlyphCoverageSummary BuildCoverageSummary(Typeface typeface)
    {
        var codePoints = new List<uint>();
        typeface.CollectUnicode(codePoints);

        var uniqueCodePoints = new HashSet<uint>(codePoints.Where(cp => cp > 0));

        return new GlyphCoverageSummary(
            GlyphCount: typeface.GlyphCount,
            MappedCodePointCount: uniqueCodePoints.Count,
            SupportsBasicLatin: HasRange(uniqueCodePoints, 0x0020, 0x007E),
            SupportsLatin1Supplement: HasRange(uniqueCodePoints, 0x00A0, 0x00FF),
            SupportsLatinExtendedA: HasRange(uniqueCodePoints, 0x0100, 0x017F),
            SupportsGreekAndCoptic: HasRange(uniqueCodePoints, 0x0370, 0x03FF),
            SupportsCyrillic: HasRange(uniqueCodePoints, 0x0400, 0x04FF),
            SupportsCjkUnifiedIdeographs: HasRange(uniqueCodePoints, 0x4E00, 0x9FFF));
    }

    private static bool HasRange(HashSet<uint> codePoints, uint startInclusive, uint endInclusive)
    {
        foreach (var codePoint in codePoints)
        {
            if (codePoint >= startInclusive && codePoint <= endInclusive)
            {
                return true;
            }
        }

        return false;
    }

    private static FontWidthClass MapWidthClass(ushort value) => value switch
    {
        1 => FontWidthClass.UltraCondensed,
        2 => FontWidthClass.ExtraCondensed,
        3 => FontWidthClass.Condensed,
        4 => FontWidthClass.SemiCondensed,
        5 => FontWidthClass.Normal,
        6 => FontWidthClass.SemiExpanded,
        7 => FontWidthClass.Expanded,
        8 => FontWidthClass.ExtraExpanded,
        9 => FontWidthClass.UltraExpanded,
        _ => FontWidthClass.Unknown
    };

    private static FontContainerFormat ResolveFormat(string extension, bool isCollection)
    {
        if (isCollection)
        {
            return FontContainerFormat.TrueTypeCollection;
        }

        return extension.ToLowerInvariant() switch
        {
            ".ttf" => FontContainerFormat.TrueType,
            ".otf" => FontContainerFormat.OpenType,
            ".woff" => FontContainerFormat.WebOpenFont,
            ".woff2" => FontContainerFormat.WebOpenFont2,
            ".ttc" => FontContainerFormat.TrueTypeCollection,
            _ => FontContainerFormat.Unknown
        };
    }

    private static void EnsureWebFontDecompressHandlers()
    {
        if (_webFontHandlersConfigured)
        {
            return;
        }

        lock (WebFontHandlerLock)
        {
            if (_webFontHandlersConfigured)
            {
                return;
            }

            WoffDefaultZlibDecompressFunc.DecompressHandler ??= TryDecompressWoff;
            Woff2DefaultBrotliDecompressFunc.DecompressHandler ??= TryDecompressWoff2;

            _webFontHandlersConfigured = true;
        }
    }

    private static bool TryDecompressWoff(byte[] compressed, byte[] decompressed)
    {
        try
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress, leaveOpen: false);

            var bytesRead = 0;
            while (bytesRead < decompressed.Length)
            {
                var read = zlib.Read(decompressed, bytesRead, decompressed.Length - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            return bytesRead == decompressed.Length;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecompressWoff2(byte[] compressed, Stream output)
    {
        long originalPosition = 0;
        long originalLength = 0;

        if (output.CanSeek)
        {
            originalPosition = output.Position;
            originalLength = output.Length;
        }

        for (var offset = 0; offset <= 8 && offset < compressed.Length; offset++)
        {
            try
            {
                if (output.CanSeek)
                {
                    output.Position = originalPosition;
                    output.SetLength(originalLength);
                }

                using var input = new MemoryStream(compressed, offset, compressed.Length - offset, writable: false);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: false);
                brotli.CopyTo(output);
                return true;
            }
            catch
            {
                // try next offset
            }
        }

        return false;
    }
}
