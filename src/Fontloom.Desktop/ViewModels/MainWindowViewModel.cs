using Fontloom.Core.Fonts;
using Fontloom.Desktop.Services;

namespace Fontloom.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly FontClassification[] AllClassifications =
    [
        FontClassification.Serif,
        FontClassification.SansSerif,
        FontClassification.Monospace,
        FontClassification.Display,
        FontClassification.Unknown
    ];

    private readonly IFontCatalogService _fontCatalogService;

    private FontIndex _fontIndex = FontIndex.Create(Array.Empty<FontInfo>());
    private IReadOnlyList<FontTileViewModel> _filteredFonts = Array.Empty<FontTileViewModel>();
    private FontTileViewModel? _selectedFont;
    private string _sampleText = "The quick brown fox jumps over the lazy dog 0123456789";
    private string _familySearch = string.Empty;
    private double _previewSize = 32;
    private int _previewWeight = 400;
    private bool _showSerif = true;
    private bool _showSansSerif = true;
    private bool _showMonospace = true;
    private bool _showDisplay = true;
    private bool _showUnknown = true;
    private bool _italicOnly;
    private bool _monospaceOnly;
    private bool _requireGlyphCoverage;
    private int _totalFontCount;
    private string _statusMessage = "Ready";

    public MainWindowViewModel(IFontCatalogService fontCatalogService, bool autoLoad = true)
    {
        _fontCatalogService = fontCatalogService ?? throw new ArgumentNullException(nameof(fontCatalogService));

        if (autoLoad)
        {
            ReloadFonts();
        }
    }

    public string SampleText
    {
        get => _sampleText;
        set
        {
            if (SetProperty(ref _sampleText, value))
            {
                if (RequireGlyphCoverage)
                {
                    ApplyFilters();
                }
            }
        }
    }

    public string FamilySearch
    {
        get => _familySearch;
        set
        {
            if (SetProperty(ref _familySearch, value))
            {
                ApplyFilters();
            }
        }
    }

    public double PreviewSize
    {
        get => _previewSize;
        set
        {
            var clamped = Math.Clamp(value, 10, 96);
            if (SetProperty(ref _previewSize, clamped))
            {
                OnPropertyChanged(nameof(PreviewSizeLabel));
            }
        }
    }

    public int PreviewWeight
    {
        get => _previewWeight;
        set
        {
            var clamped = Math.Clamp(value, 100, 900);
            if (SetProperty(ref _previewWeight, clamped))
            {
                OnPropertyChanged(nameof(PreviewWeightLabel));
            }
        }
    }

    public bool ShowSerif
    {
        get => _showSerif;
        set => SetFilterToggle(ref _showSerif, value);
    }

    public bool ShowSansSerif
    {
        get => _showSansSerif;
        set => SetFilterToggle(ref _showSansSerif, value);
    }

    public bool ShowMonospace
    {
        get => _showMonospace;
        set => SetFilterToggle(ref _showMonospace, value);
    }

    public bool ShowDisplay
    {
        get => _showDisplay;
        set => SetFilterToggle(ref _showDisplay, value);
    }

    public bool ShowUnknown
    {
        get => _showUnknown;
        set => SetFilterToggle(ref _showUnknown, value);
    }

    public bool ItalicOnly
    {
        get => _italicOnly;
        set => SetFilterToggle(ref _italicOnly, value);
    }

    public bool MonospaceOnly
    {
        get => _monospaceOnly;
        set => SetFilterToggle(ref _monospaceOnly, value);
    }

    public bool RequireGlyphCoverage
    {
        get => _requireGlyphCoverage;
        set => SetFilterToggle(ref _requireGlyphCoverage, value);
    }

    public IReadOnlyList<FontTileViewModel> FilteredFonts
    {
        get => _filteredFonts;
        private set
        {
            if (SetProperty(ref _filteredFonts, value))
            {
                OnPropertyChanged(nameof(VisibleFontCountLabel));
            }
        }
    }

    public FontTileViewModel? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetProperty(ref _selectedFont, value))
            {
                RaiseSelectionChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string PreviewSizeLabel => $"{PreviewSize:0} pt";

    public string PreviewWeightLabel => PreviewWeight.ToString();

    public string VisibleFontCountLabel => $"{FilteredFonts.Count} shown";

    public string TotalFontCountLabel => $"{_totalFontCount} indexed";

    public string SelectedFamily => SelectedFont?.Family ?? "No font selected";

    public string SelectedSubfamily => SelectedFont?.Subfamily ?? "—";

    public string SelectedFormat => SelectedFont?.Format ?? "—";

    public string SelectedWeightAndWidth
    {
        get
        {
            if (SelectedFont is null)
            {
                return "—";
            }

            return $"{SelectedFont.Weight} / {SelectedFont.Width}";
        }
    }

    public string SelectedStyle
    {
        get
        {
            if (SelectedFont is null)
            {
                return "—";
            }

            return SelectedFont.IsItalic ? "Italic" : "Upright";
        }
    }

    public string SelectedCoverageSummary
    {
        get
        {
            if (SelectedFont is null)
            {
                return "—";
            }

            return $"{SelectedFont.MappedCodePointCount:N0} mapped code points across {SelectedFont.GlyphCount:N0} glyphs";
        }
    }

    public string SelectedSourcePath => SelectedFont?.SourcePath ?? "—";

    public void ReloadFonts(CancellationToken cancellationToken = default)
    {
        try
        {
            StatusMessage = "Indexing system fonts…";
            _fontIndex = _fontCatalogService.BuildIndex(cancellationToken);
            _totalFontCount = _fontIndex.Count;

            OnPropertyChanged(nameof(TotalFontCountLabel));
            ApplyFilters();

            StatusMessage = $"Loaded {_totalFontCount} fonts.";
        }
        catch (Exception ex)
        {
            _fontIndex = FontIndex.Create(Array.Empty<FontInfo>());
            _totalFontCount = 0;
            OnPropertyChanged(nameof(TotalFontCountLabel));
            FilteredFonts = Array.Empty<FontTileViewModel>();
            SelectedFont = null;
            StatusMessage = $"Failed to load fonts: {ex.Message}";
        }
    }

    private void ApplyFilters()
    {
        var classifications = GetEnabledClassifications();

        if (classifications.Count == 0)
        {
            FilteredFonts = Array.Empty<FontTileViewModel>();
            SelectedFont = null;
            StatusMessage = "No classifications enabled.";
            return;
        }

        IReadOnlyCollection<FontClassification>? classificationFilter =
            classifications.Count == AllClassifications.Length ? null : classifications;

        var query = new FontIndexQuery(
            FamilyNameContains: string.IsNullOrWhiteSpace(FamilySearch) ? null : FamilySearch.Trim(),
            Classifications: classificationFilter,
            IsItalic: ItalicOnly ? true : null,
            IsMonospace: MonospaceOnly ? true : null,
            SupportsText: RequireGlyphCoverage ? SampleText : null);

        var filteredFonts = _fontIndex.Query(query)
            .Select(FontTileViewModel.FromFontInfo)
            .ToArray();

        FilteredFonts = filteredFonts;

        if (filteredFonts.Length == 0)
        {
            SelectedFont = null;
            StatusMessage = $"0 of {_totalFontCount} fonts match filters.";
            return;
        }

        if (SelectedFont is null ||
            filteredFonts.All(font => !StringComparer.OrdinalIgnoreCase.Equals(font.SourcePath, SelectedFont.SourcePath)))
        {
            SelectedFont = filteredFonts[0];
        }

        StatusMessage = $"{filteredFonts.Length} of {_totalFontCount} fonts match filters.";
    }

    private List<FontClassification> GetEnabledClassifications()
    {
        var result = new List<FontClassification>(capacity: AllClassifications.Length);

        if (ShowSerif)
        {
            result.Add(FontClassification.Serif);
        }

        if (ShowSansSerif)
        {
            result.Add(FontClassification.SansSerif);
        }

        if (ShowMonospace)
        {
            result.Add(FontClassification.Monospace);
        }

        if (ShowDisplay)
        {
            result.Add(FontClassification.Display);
        }

        if (ShowUnknown)
        {
            result.Add(FontClassification.Unknown);
        }

        return result;
    }

    private void SetFilterToggle(ref bool field, bool value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            ApplyFilters();
        }
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedFamily));
        OnPropertyChanged(nameof(SelectedSubfamily));
        OnPropertyChanged(nameof(SelectedFormat));
        OnPropertyChanged(nameof(SelectedWeightAndWidth));
        OnPropertyChanged(nameof(SelectedStyle));
        OnPropertyChanged(nameof(SelectedCoverageSummary));
        OnPropertyChanged(nameof(SelectedSourcePath));
    }
}
