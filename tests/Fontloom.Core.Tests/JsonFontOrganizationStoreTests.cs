using FluentAssertions;
using Fontloom.Core.Organization;

namespace Fontloom.Core.Tests;

public class JsonFontOrganizationStoreTests
{
    [Fact]
    public void Store_Crud_PersistsFavoritesTagsCollectionsAndLooseFoldersAcrossInstances()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var storagePath = Path.Combine(tempDirectory.FullName, "font-organization.json");
            var fontPath = Path.Combine(tempDirectory.FullName, "fixtures", "Inter-Regular.ttf");
            var looseFolder = Path.Combine(tempDirectory.FullName, "loose-fonts");
            Directory.CreateDirectory(Path.GetDirectoryName(fontPath)!);
            Directory.CreateDirectory(looseFolder);

            var store = new JsonFontOrganizationStore(storagePath);

            store.SetFavorite(fontPath, true).Should().BeTrue();
            store.SetFavorite(fontPath, true).Should().BeFalse();
            store.SetFavorite(fontPath, false).Should().BeTrue();
            store.SetFavorite(fontPath, true).Should().BeTrue();

            store.AddTag(fontPath, "display").Should().BeTrue();
            store.AddTag(fontPath, "headline").Should().BeTrue();
            store.RemoveTag(fontPath, "headline").Should().BeTrue();

            store.CreateCollection("Brand kit").Should().BeTrue();
            store.AddFontToCollection("Brand kit", fontPath).Should().BeTrue();
            store.AddLooseFontFolder(looseFolder).Should().BeTrue();

            var reloaded = new JsonFontOrganizationStore(storagePath);
            var snapshot = reloaded.GetSnapshot();

            snapshot.FavoriteFontPaths.Should().ContainSingle(path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, Path.GetFullPath(fontPath)));

            snapshot.TagsByFontPath.Should().ContainKey(Path.GetFullPath(fontPath));
            snapshot.TagsByFontPath[Path.GetFullPath(fontPath)]
                .Should().BeEquivalentTo(new[] { "display" });

            snapshot.Collections.Should().ContainKey("Brand kit");
            snapshot.Collections["Brand kit"].Should().ContainSingle(path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, Path.GetFullPath(fontPath)));

            snapshot.LooseFontFolders.Should().ContainSingle(path =>
                StringComparer.OrdinalIgnoreCase.Equals(path, Path.GetFullPath(looseFolder)));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void SetTags_ReplacesTagSet_AndRemovesEntryWhenEmpty()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var storagePath = Path.Combine(tempDirectory.FullName, "font-organization.json");
            var fontPath = Path.Combine(tempDirectory.FullName, "Inter-Regular.ttf");

            var store = new JsonFontOrganizationStore(storagePath);

            store.SetTags(fontPath, new[] { "display", "body" }).Should().BeTrue();
            store.SetTags(fontPath, new[] { "DISPLAY", "body" }).Should().BeFalse();
            store.SetTags(fontPath, new[] { "body", "ui" }).Should().BeTrue();

            var snapshot = store.GetSnapshot();
            snapshot.TagsByFontPath[Path.GetFullPath(fontPath)]
                .Should().BeEquivalentTo(new[] { "body", "ui" });

            store.SetTags(fontPath, Array.Empty<string>()).Should().BeTrue();
            store.GetSnapshot().TagsByFontPath.Should().NotContainKey(Path.GetFullPath(fontPath));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LooseFolderCrud_IsDistinctAndCaseInsensitive()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var storagePath = Path.Combine(tempDirectory.FullName, "font-organization.json");
            var folderPath = Path.Combine(tempDirectory.FullName, "LooseFonts");
            Directory.CreateDirectory(folderPath);

            var store = new JsonFontOrganizationStore(storagePath);

            store.AddLooseFontFolder(folderPath).Should().BeTrue();
            store.AddLooseFontFolder(folderPath.ToUpperInvariant()).Should().BeFalse();

            var snapshot = store.GetSnapshot();
            snapshot.LooseFontFolders.Should().ContainSingle();

            store.RemoveLooseFontFolder(folderPath.ToUpperInvariant()).Should().BeTrue();
            store.GetSnapshot().LooseFontFolders.Should().BeEmpty();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
