namespace Fontloom.Core.Organization;

public interface IFontOrganizationStore
{
    FontOrganizationSnapshot GetSnapshot();

    bool SetFavorite(string fontPath, bool isFavorite);

    bool SetTags(string fontPath, IEnumerable<string> tags);

    bool AddTag(string fontPath, string tag);

    bool RemoveTag(string fontPath, string tag);

    bool CreateCollection(string collectionName);

    bool DeleteCollection(string collectionName);

    bool AddFontToCollection(string collectionName, string fontPath);

    bool RemoveFontFromCollection(string collectionName, string fontPath);

    bool AddLooseFontFolder(string folderPath);

    bool RemoveLooseFontFolder(string folderPath);
}
