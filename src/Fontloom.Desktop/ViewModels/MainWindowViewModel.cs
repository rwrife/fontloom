using Fontloom.Core.Fonts;
using Fontloom.Core.Organization;
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

    private readonly IFontCatalogService _fontCatalogService;
    private readonly IFontOrganizationStore _organizationStore;

    private readonly HashSet<string> _favoritePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _tagsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _collections = new(StringComparer.OrdinalIgnoreCase);

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
    private bool _showFavoritesOnly;
    private int _totalFontCount;
    private string _statusMessage = "Ready";

    private IReadOnlyList<string> _tagFacetOptions = new[] { AllTagsFacet };
    private string _activeTagFacet = AllTagsFacet;

    private bool _isSelectedFontFavorite;
    private string _selectedFontTagsEditor = string.Empty;

    private string _newCollectionName = string.Empty;
    private IReadOnlyList<string> _collectionNames = Array.Empty<string>();
    private string? _selectedCollectionName;
    private bool _isSelectedFontInSelectedCollection;
    private string _selectedFontCollectionsLabel = "—";

    private string _looseFontFolderInput = string.Empty;
    private IReadOnlyList<string> _looseFontFolders = Array.Empty<string>();
    private string? _selectedLooseFontFolder;

    public MainWindowViewModel(
        IFontCatalogService fontCatalogService,
        IFontOrganizationStore? organizationStore = null,
        bool autoLoad = true)
    {
        _fontCatalogService = fontCatalogService ?? throw new ArgumentNullException(nameof(fontCatalogService));
        _organizationStore = organizationStore ?? new InMemoryFontOrganizationStore();

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

    public FontTileViewModel? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (SetProperty(ref _selectedFont, value))
            {
                RaiseSelectionChanged();
                RefreshSelectedFontMetadata();
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

    public string FavoriteButtonLabel => IsSelectedFontFavorite ? "★ Unfavorite" : "☆ Favorite";

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
            ReloadOrganizationState();

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
            SelectedFontTagsEditor = string.Empty;
            IsSelectedFontInSelectedCollection = false;
            SelectedFontCollectionsLabel = "—";
            return;
        }

        var normalizedPath = NormalizePath(SelectedFont.SourcePath);

        IsSelectedFontFavorite = _favoritePaths.Contains(normalizedPath);

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
