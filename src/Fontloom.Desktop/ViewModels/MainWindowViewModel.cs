using Fontloom.Core.Ai;
using Fontloom.Core.Fonts;
using Fontloom.Core.Organization;
using Fontloom.Core.Specimens;
using Fontloom.Desktop.Services;

namespace Fontloom.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const string AllTagsFacet = "All tags";

    private static readonly FontClassification[] AllClassifications =
    [
        FontClassification.Serif,
        FontClassification.SansSerif,
        FontClassification.Monospace,
        FontClassification.Display,
        FontClassification.Unknown
    ];

    private const int ComparisonTrayMinimum = 2;
    private const int ComparisonTrayMaximum = 6;

    private readonly IFontCatalogService _fontCatalogService;
    private readonly IFontAiService _fontAiService;
    private readonly IFontOrganizationStore _organizationStore;
    private readonly ISpecimenExporter _specimenExporter;

    private readonly HashSet<string> _favoritePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _tagsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _comparisonTrayPaths = new();

    private FontIndex _fontIndex = FontIndex.Create(Array.Empty<FontInfo>());
    private IReadOnlyList<FontTileViewModel> _filteredFonts = Array.Empty<FontTileViewModel>();
    private IReadOnlyList<FontTileViewModel> _comparisonTrayFonts = Array.Empty<FontTileViewModel>();
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
    private bool _showFavoritesOnly;
    private int _totalFontCount;
    private string _statusMessage = "Ready";

    private IReadOnlyList<string> _tagFacetOptions = new[] { AllTagsFacet };
    private string _activeTagFacet = AllTagsFacet;

    private bool _isSelectedFontFavorite;
    private bool _isSelectedFontPinnedForComparison;
    private string _selectedFontTagsEditor = string.Empty;

    private string _newCollectionName = string.Empty;
    private IReadOnlyList<string> _collectionNames = Array.Empty<string>();
    private string? _selectedCollectionName;
    private bool _isSelectedFontInSelectedCollection;
    private string _selectedFontCollectionsLabel = "—";

    private string _looseFontFolderInput = string.Empty;
    private IReadOnlyList<string> _looseFontFolders = Array.Empty<string>();
    private string? _selectedLooseFontFolder;

    private string _exportDirectoryInput = BuildDefaultExportDirectory();
    private string _exportFileStemInput = "specimen";

    private bool _localAiEnabled;
    private string _localAiEndpoint = LocalFontAiOptions.DefaultEndpoint;
    private bool _isLocalAiEndpointReachable;
    private string _localAiStatus = "Local AI is disabled.";
    private string _pairingModeLabel = "Enable Local AI to get pairing suggestions.";
    private IReadOnlyList<string> _pairingSuggestions = Array.Empty<string>();
    private string _selectedFontAutoDescription = "—";

    public MainWindowViewModel(
        IFontCatalogService fontCatalogService,
        IFontOrganizationStore? organizationStore = null,
        ISpecimenExporter? specimenExporter = null,
        IFontAiService? fontAiService = null,
        bool autoLoad = true)
    {
        _fontCatalogService = fontCatalogService ?? throw new ArgumentNullException(nameof(fontCatalogService));
        _organizationStore = organizationStore ?? new InMemoryFontOrganizationStore();
        _specimenExporter = specimenExporter ?? new SkiaSpecimenExporter();
        _fontAiService = fontAiService ?? new LocalFontAiService();

        ReloadOrganizationState();

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

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set => SetFilterToggle(ref _showFavoritesOnly, value);
    }

    public IReadOnlyList<string> TagFacetOptions
    {
        get => _tagFacetOptions;
        private set => SetProperty(ref _tagFacetOptions, value);
    }

    public string ActiveTagFacet
    {
        get => _activeTagFacet;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? AllTagsFacet : value.Trim();
            if (SetProperty(ref _activeTagFacet, normalized))
            {
                ApplyFilters();
            }
        }
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

    public IReadOnlyList<FontTileViewModel> ComparisonTrayFonts
    {
        get => _comparisonTrayFonts;
        private set
        {
            if (SetProperty(ref _comparisonTrayFonts, value))
            {
                OnPropertyChanged(nameof(ComparisonTrayStatusLabel));
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
                RefreshSelectedFontMetadata();
                RefreshPairingSuggestions();
            }
        }
    }

    public bool IsSelectedFontFavorite
    {
        get => _isSelectedFontFavorite;
        private set
        {
            if (SetProperty(ref _isSelectedFontFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteButtonLabel));
            }
        }
    }

    public bool IsSelectedFontPinnedForComparison
    {
        get => _isSelectedFontPinnedForComparison;
        private set
        {
            if (SetProperty(ref _isSelectedFontPinnedForComparison, value))
            {
                OnPropertyChanged(nameof(ComparisonPinButtonLabel));
            }
        }
    }

    public string FavoriteButtonLabel => IsSelectedFontFavorite ? "★ Unfavorite" : "☆ Favorite";

    public string ComparisonPinButtonLabel => IsSelectedFontPinnedForComparison
        ? "Unpin from compare tray"
        : "Pin to compare tray";

    public string SelectedFontTagsEditor
    {
        get => _selectedFontTagsEditor;
        set => SetProperty(ref _selectedFontTagsEditor, value);
    }

    public string NewCollectionName
    {
        get => _newCollectionName;
        set => SetProperty(ref _newCollectionName, value);
    }

    public IReadOnlyList<string> CollectionNames
    {
        get => _collectionNames;
        private set => SetProperty(ref _collectionNames, value);
    }

    public string? SelectedCollectionName
    {
        get => _selectedCollectionName;
        set
        {
            if (SetProperty(ref _selectedCollectionName, value))
            {
                RefreshSelectedFontMetadata();
            }
        }
    }

    public bool IsSelectedFontInSelectedCollection
    {
        get => _isSelectedFontInSelectedCollection;
        private set
        {
            if (SetProperty(ref _isSelectedFontInSelectedCollection, value))
            {
                OnPropertyChanged(nameof(CollectionMembershipButtonLabel));
            }
        }
    }

    public string CollectionMembershipButtonLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedCollectionName))
            {
                return "Select a collection";
            }

            return IsSelectedFontInSelectedCollection
                ? "Remove from collection"
                : "Add to collection";
        }
    }

    public string SelectedFontCollectionsLabel
    {
        get => _selectedFontCollectionsLabel;
        private set => SetProperty(ref _selectedFontCollectionsLabel, value);
    }

    public string LooseFontFolderInput
    {
        get => _looseFontFolderInput;
        set => SetProperty(ref _looseFontFolderInput, value);
    }

    public IReadOnlyList<string> LooseFontFolders
    {
        get => _looseFontFolders;
        private set => SetProperty(ref _looseFontFolders, value);
    }

    public string? SelectedLooseFontFolder
    {
        get => _selectedLooseFontFolder;
        set => SetProperty(ref _selectedLooseFontFolder, value);
    }

    public string ExportDirectoryInput
    {
        get => _exportDirectoryInput;
        set => SetProperty(ref _exportDirectoryInput, value);
    }

    public string ExportFileStemInput
    {
        get => _exportFileStemInput;
        set => SetProperty(ref _exportFileStemInput, value);
    }

    public bool LocalAiEnabled
    {
        get => _localAiEnabled;
        set
        {
            if (SetProperty(ref _localAiEnabled, value))
            {
                RefreshPairingSuggestions();
            }
        }
    }

    public string LocalAiEndpoint
    {
        get => _localAiEndpoint;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? LocalFontAiOptions.DefaultEndpoint
                : value.Trim();

            if (SetProperty(ref _localAiEndpoint, normalized))
            {
                if (LocalAiEnabled)
                {
                    RefreshPairingSuggestions();
                }
            }
        }
    }

    public bool IsLocalAiEndpointReachable
    {
        get => _isLocalAiEndpointReachable;
        private set => SetProperty(ref _isLocalAiEndpointReachable, value);
    }

    public string LocalAiStatus
    {
        get => _localAiStatus;
        private set => SetProperty(ref _localAiStatus, value);
    }

    public string PairingModeLabel
    {
        get => _pairingModeLabel;
        private set => SetProperty(ref _pairingModeLabel, value);
    }

    public IReadOnlyList<string> PairingSuggestions
    {
        get => _pairingSuggestions;
        private set => SetProperty(ref _pairingSuggestions, value);
    }

    public string SelectedFontAutoDescription
    {
        get => _selectedFontAutoDescription;
        private set => SetProperty(ref _selectedFontAutoDescription, value);
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

    public string ComparisonTrayStatusLabel
    {
        get
        {
            var pinnedCount = ComparisonTrayFonts.Count;

            return pinnedCount switch
            {
                0 => $"Pin {ComparisonTrayMinimum}–{ComparisonTrayMaximum} fonts to compare side by side.",
                < ComparisonTrayMinimum => $"{pinnedCount} pinned (add {ComparisonTrayMinimum - pinnedCount} more to compare).",
                _ => $"{pinnedCount} pinned and synchronized with sample text/size/weight."
            };
        }
    }

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
            ReloadOrganizationState();

            StatusMessage = "Indexing system fonts…";
            _fontIndex = _fontCatalogService.BuildIndex(cancellationToken);
            _totalFontCount = _fontIndex.Count;

            OnPropertyChanged(nameof(TotalFontCountLabel));
            ApplyFilters();
            RefreshPairingSuggestions();

            StatusMessage = $"Loaded {_totalFontCount} fonts.";
        }
        catch (Exception ex)
        {
            _fontIndex = FontIndex.Create(Array.Empty<FontInfo>());
            _totalFontCount = 0;
            OnPropertyChanged(nameof(TotalFontCountLabel));
            FilteredFonts = Array.Empty<FontTileViewModel>();
            RefreshComparisonTrayState();
            SelectedFont = null;
            PairingSuggestions = Array.Empty<string>();
            SelectedFontAutoDescription = "—";
            PairingModeLabel = "Enable Local AI to get pairing suggestions.";
            LocalAiStatus = "Local AI is disabled.";
            IsLocalAiEndpointReachable = false;
            StatusMessage = $"Failed to load fonts: {ex.Message}";
        }
    }

    public void ToggleSelectedFavorite()
    {
        if (SelectedFont is null)
        {
            return;
        }

        var normalizedPath = NormalizePath(SelectedFont.SourcePath);
        var shouldFavorite = !_favoritePaths.Contains(normalizedPath);

        if (_organizationStore.SetFavorite(normalizedPath, shouldFavorite))
        {
            ReloadOrganizationState();
            ApplyFilters();
            StatusMessage = shouldFavorite
                ? "Font added to favorites."
                : "Font removed from favorites.";
        }
    }

    public void ToggleSelectedFontComparisonPin()
    {
        if (SelectedFont is null)
        {
            return;
        }

        var normalizedPath = NormalizePath(SelectedFont.SourcePath);

        if (ComparisonTrayContains(normalizedPath))
        {
            _comparisonTrayPaths.RemoveAll(path => StringComparer.OrdinalIgnoreCase.Equals(path, normalizedPath));
            RefreshComparisonTrayState();
            StatusMessage = "Font removed from comparison tray.";
            return;
        }

        if (_comparisonTrayPaths.Count >= ComparisonTrayMaximum)
        {
            StatusMessage = $"Comparison tray is full ({ComparisonTrayMaximum} fonts max).";
            return;
        }

        _comparisonTrayPaths.Add(normalizedPath);
        RefreshComparisonTrayState();
        StatusMessage = "Font pinned to comparison tray.";
    }

    public void ExportSelectedFontSpecimenPng()
    {
        if (SelectedFont is null)
        {
            StatusMessage = "Select a font before exporting a PNG specimen.";
            return;
        }

        try
        {
            var outputPath = BuildExportPath($"{BuildSafeFileStem(SelectedFont.Family)}-font", "png");
            _specimenExporter.ExportFontPng(SelectedFont.Font, outputPath, BuildSpecimenOptions());
            StatusMessage = $"Exported PNG specimen: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PNG export failed: {ex.Message}";
        }
    }

    public void ExportSelectedCollectionSpecimenPdf()
    {
        if (string.IsNullOrWhiteSpace(SelectedCollectionName))
        {
            StatusMessage = "Select a collection before exporting a PDF specimen.";
            return;
        }

        if (!_collections.TryGetValue(SelectedCollectionName, out var collectionMembers) || collectionMembers.Count == 0)
        {
            StatusMessage = "Selected collection has no fonts to export.";
            return;
        }

        var fontsByPath = _fontIndex.Query()
            .GroupBy(font => NormalizePath(font.SourcePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var fontsToExport = collectionMembers
            .Select(path => fontsByPath.TryGetValue(path, out var font) ? font : null)
            .Where(font => font is not null)
            .Cast<FontInfo>()
            .ToArray();

        if (fontsToExport.Length == 0)
        {
            StatusMessage = "Collection fonts are not currently indexed, so export cannot proceed.";
            return;
        }

        try
        {
            var outputPath = BuildExportPath($"{BuildSafeFileStem(SelectedCollectionName)}-collection", "pdf");
            var options = BuildSpecimenOptions() with { CollectionLabel = SelectedCollectionName };
            _specimenExporter.ExportCollectionPdf(fontsToExport, outputPath, options);
            StatusMessage = $"Exported PDF specimen: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF export failed: {ex.Message}";
        }
    }

    public void SaveSelectedFontTags()
    {
        if (SelectedFont is null)
        {
            return;
        }

        var normalizedPath = NormalizePath(SelectedFont.SourcePath);
        var tags = ParseTagEditor(SelectedFontTagsEditor);

        if (_organizationStore.SetTags(normalizedPath, tags))
        {
            ReloadOrganizationState();
            ApplyFilters();
            StatusMessage = tags.Count == 0
                ? "Cleared tags for selected font."
                : "Updated tags for selected font.";
        }
    }

    public void CreateCollection()
    {
        var collectionName = NewCollectionName.Trim();
        if (collectionName.Length == 0)
        {
            StatusMessage = "Collection name cannot be empty.";
            return;
        }

        if (_organizationStore.CreateCollection(collectionName))
        {
            ReloadOrganizationState();
            SelectedCollectionName = CollectionNames
                .FirstOrDefault(name => StringComparer.OrdinalIgnoreCase.Equals(name, collectionName));
            NewCollectionName = string.Empty;
            StatusMessage = "Collection created.";
            return;
        }

        StatusMessage = "Collection already exists.";
    }

    public void ToggleSelectedFontCollectionMembership()
    {
        if (SelectedFont is null || string.IsNullOrWhiteSpace(SelectedCollectionName))
        {
            return;
        }

        var collectionName = SelectedCollectionName;
        var normalizedPath = NormalizePath(SelectedFont.SourcePath);

        var wasInCollection = IsSelectedFontInSelectedCollection;

        var changed = wasInCollection
            ? _organizationStore.RemoveFontFromCollection(collectionName, normalizedPath)
            : _organizationStore.AddFontToCollection(collectionName, normalizedPath);

        if (!changed)
        {
            return;
        }

        ReloadOrganizationState();
        ApplyFilters();
        StatusMessage = wasInCollection
            ? "Font removed from collection."
            : "Font added to collection.";
    }

    public void AddLooseFontFolder()
    {
        var folderInput = LooseFontFolderInput.Trim();
        if (folderInput.Length == 0)
        {
            StatusMessage = "Folder path cannot be empty.";
            return;
        }

        if (!Directory.Exists(folderInput))
        {
            StatusMessage = "Loose-font folder does not exist.";
            return;
        }

        if (_organizationStore.AddLooseFontFolder(folderInput))
        {
            LooseFontFolderInput = string.Empty;
            ReloadOrganizationState();
            ReloadFonts();
            StatusMessage = "Added loose-font folder and refreshed index incrementally.";
            return;
        }

        StatusMessage = "Loose-font folder is already tracked.";
    }

    public void RemoveSelectedLooseFontFolder()
    {
        if (string.IsNullOrWhiteSpace(SelectedLooseFontFolder))
        {
            return;
        }

        if (_organizationStore.RemoveLooseFontFolder(SelectedLooseFontFolder))
        {
            ReloadOrganizationState();
            ReloadFonts();
            StatusMessage = "Removed loose-font folder and refreshed index incrementally.";
        }
    }

    private void RefreshPairingSuggestions()
    {
        PairingSuggestions = Array.Empty<string>();
        SelectedFontAutoDescription = "—";
        IsLocalAiEndpointReachable = false;

        if (!LocalAiEnabled)
        {
            PairingModeLabel = "Enable Local AI to get pairing suggestions.";
            LocalAiStatus = "Local AI is disabled.";
            return;
        }

        if (SelectedFont is null)
        {
            PairingModeLabel = "Select a font to get pairing suggestions.";
            LocalAiStatus = "Local AI is enabled.";
            return;
        }

        var libraryFonts = _fontIndex.Query();

        try
        {
            var result = _fontAiService
                .SuggestPairingsAsync(
                    SelectedFont.Font,
                    libraryFonts,
                    enableLocalAi: true,
                    endpoint: LocalAiEndpoint)
                .GetAwaiter()
                .GetResult();

            IsLocalAiEndpointReachable = result.EndpointReachable;
            PairingModeLabel = result.UsedFallback
                ? "Heuristic fallback"
                : "Local AI suggestions";
            LocalAiStatus = result.EndpointReachable
                ? $"Connected to {LocalAiEndpoint}."
                : $"Could not reach {LocalAiEndpoint}; using local heuristics.";

            SelectedFontAutoDescription = string.IsNullOrWhiteSpace(result.Description)
                ? "—"
                : result.Description.Trim();

            PairingSuggestions = result.Pairings
                .Take(3)
                .Select((pairing, index) =>
                    $"{index + 1}. {pairing.Font.Family} {pairing.Font.Subfamily} — {pairing.Rationale}")
                .ToArray();

            if (PairingSuggestions.Count == 0)
            {
                PairingModeLabel = "No pairing candidates found.";
            }
        }
        catch (Exception ex)
        {
            var fallback = HeuristicFontPairingEngine.BuildFallback(
                SelectedFont.Font,
                libraryFonts,
                localAiEnabled: true,
                endpointReachable: false);

            PairingModeLabel = "Heuristic fallback";
            LocalAiStatus = $"Local AI request failed ({ex.Message}). Showing heuristic suggestions.";
            SelectedFontAutoDescription = fallback.Description;
            PairingSuggestions = fallback.Pairings
                .Select((pairing, index) =>
                    $"{index + 1}. {pairing.Font.Family} {pairing.Font.Subfamily} — {pairing.Rationale}")
                .ToArray();
        }
    }

    private void ApplyFilters()
    {
        var classifications = GetEnabledClassifications();

        if (classifications.Count == 0)
        {
            FilteredFonts = Array.Empty<FontTileViewModel>();
            SelectedFont = null;
            RefreshPairingSuggestions();
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

        var hasTagFacetFilter = !StringComparer.OrdinalIgnoreCase.Equals(ActiveTagFacet, AllTagsFacet);

        var filteredFonts = _fontIndex.Query(query)
            .Where(font => !ShowFavoritesOnly || IsFavorite(font.SourcePath))
            .Where(font => !hasTagFacetFilter || HasTag(font.SourcePath, ActiveTagFacet))
            .Select(font => FontTileViewModel.FromFontInfo(
                font,
                isFavorite: IsFavorite(font.SourcePath),
                tags: GetTags(font.SourcePath)))
            .ToArray();

        FilteredFonts = filteredFonts;
        RefreshComparisonTrayState();

        if (filteredFonts.Length == 0)
        {
            SelectedFont = null;
            RefreshPairingSuggestions();
            StatusMessage = $"0 of {_totalFontCount} fonts match filters.";
            return;
        }

        if (SelectedFont is null ||
            filteredFonts.All(font => !StringComparer.OrdinalIgnoreCase.Equals(font.SourcePath, SelectedFont.SourcePath)))
        {
            SelectedFont = filteredFonts[0];
        }

        RefreshPairingSuggestions();
        StatusMessage = $"{filteredFonts.Length} of {_totalFontCount} fonts match filters.";
    }

    private void ReloadOrganizationState()
    {
        var snapshot = _organizationStore.GetSnapshot();

        _favoritePaths.Clear();
        foreach (var favoritePath in snapshot.FavoriteFontPaths)
        {
            _favoritePaths.Add(NormalizePath(favoritePath));
        }

        _tagsByPath.Clear();
        foreach (var pair in snapshot.TagsByFontPath)
        {
            var normalizedPath = NormalizePath(pair.Key);
            _tagsByPath[normalizedPath] = new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        _collections.Clear();
        foreach (var pair in snapshot.Collections)
        {
            _collections[pair.Key] = new HashSet<string>(pair.Value.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
        }

        LooseFontFolders = snapshot.LooseFontFolders
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tagFacets = _tagsByPath.Values
            .SelectMany(tags => tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Prepend(AllTagsFacet)
            .ToArray();

        TagFacetOptions = tagFacets;

        if (!tagFacets.Contains(_activeTagFacet, StringComparer.OrdinalIgnoreCase))
        {
            _activeTagFacet = AllTagsFacet;
            OnPropertyChanged(nameof(ActiveTagFacet));
        }

        CollectionNames = _collections.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(SelectedCollectionName) &&
            !CollectionNames.Contains(SelectedCollectionName, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCollectionName = null;
        }

        RefreshSelectedFontMetadata();
    }

    private void RefreshSelectedFontMetadata()
    {
        if (SelectedFont is null)
        {
            IsSelectedFontFavorite = false;
            IsSelectedFontPinnedForComparison = false;
            SelectedFontTagsEditor = string.Empty;
            IsSelectedFontInSelectedCollection = false;
            SelectedFontCollectionsLabel = "—";
            return;
        }

        var normalizedPath = NormalizePath(SelectedFont.SourcePath);

        IsSelectedFontFavorite = _favoritePaths.Contains(normalizedPath);
        IsSelectedFontPinnedForComparison = ComparisonTrayContains(normalizedPath);

        var tags = GetTags(normalizedPath);
        SelectedFontTagsEditor = string.Join(", ", tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));

        var matchingCollections = _collections
            .Where(pair => pair.Value.Contains(normalizedPath))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SelectedFontCollectionsLabel = matchingCollections.Length == 0
            ? "Not in any collection"
            : string.Join(", ", matchingCollections);

        IsSelectedFontInSelectedCollection =
            !string.IsNullOrWhiteSpace(SelectedCollectionName) &&
            _collections.TryGetValue(SelectedCollectionName, out var selectedCollectionMembers) &&
            selectedCollectionMembers.Contains(normalizedPath);
    }

    private void RefreshComparisonTrayState()
    {
        if (_comparisonTrayPaths.Count == 0)
        {
            ComparisonTrayFonts = Array.Empty<FontTileViewModel>();
            IsSelectedFontPinnedForComparison = false;
            return;
        }

        var fontsByPath = _fontIndex.Query()
            .GroupBy(font => NormalizePath(font.SourcePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var nextTray = new List<FontTileViewModel>();
        var survivingPaths = new List<string>();

        foreach (var normalizedPath in _comparisonTrayPaths)
        {
            if (!fontsByPath.TryGetValue(normalizedPath, out var font))
            {
                continue;
            }

            survivingPaths.Add(normalizedPath);
            nextTray.Add(FontTileViewModel.FromFontInfo(
                font,
                isFavorite: IsFavorite(font.SourcePath),
                tags: GetTags(font.SourcePath)));
        }

        _comparisonTrayPaths.Clear();
        _comparisonTrayPaths.AddRange(survivingPaths);

        ComparisonTrayFonts = nextTray;

        if (SelectedFont is not null)
        {
            IsSelectedFontPinnedForComparison = ComparisonTrayContains(NormalizePath(SelectedFont.SourcePath));
        }
    }

    private bool ComparisonTrayContains(string normalizedPath)
        => _comparisonTrayPaths.Any(path => StringComparer.OrdinalIgnoreCase.Equals(path, normalizedPath));

    private SpecimenExportOptions BuildSpecimenOptions()
        => new(
            SampleText: string.IsNullOrWhiteSpace(SampleText) ? SpecimenExportOptions.Default.SampleText : SampleText,
            PointSize: (float)Math.Clamp(PreviewSize, 12, 96));

    private string BuildExportPath(string defaultStem, string extension)
    {
        var exportDirectory = ResolveExportDirectory();
        var stem = BuildSafeFileStem(string.IsNullOrWhiteSpace(ExportFileStemInput) ? defaultStem : ExportFileStemInput);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = BuildSafeFileStem(defaultStem);
        }

        var fileName = $"{stem}.{extension.TrimStart('.')}";
        return Path.Combine(exportDirectory, fileName);
    }

    private string ResolveExportDirectory()
    {
        var input = ExportDirectoryInput.Trim();
        if (input.Length == 0)
        {
            input = BuildDefaultExportDirectory();
            ExportDirectoryInput = input;
        }

        return Path.GetFullPath(input);
    }

    private static string BuildSafeFileStem(string value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "specimen" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(candidate
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .ToArray())
            .Trim();

        return sanitized.Length == 0 ? "specimen" : sanitized;
    }

    private static string BuildDefaultExportDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Environment.CurrentDirectory;
        }

        return Path.Combine(baseDir, "fontloom-specimens");
    }

    private bool IsFavorite(string sourcePath)
        => _favoritePaths.Contains(NormalizePath(sourcePath));

    private bool HasTag(string sourcePath, string tag)
    {
        var normalizedPath = NormalizePath(sourcePath);

        if (!_tagsByPath.TryGetValue(normalizedPath, out var tags))
        {
            return false;
        }

        return tags.Contains(tag);
    }

    private IReadOnlyCollection<string> GetTags(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);

        if (_tagsByPath.TryGetValue(normalizedPath, out var tags))
        {
            return tags
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyCollection<string> ParseTagEditor(string editorText)
        => editorText
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
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
