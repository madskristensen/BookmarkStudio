namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkNameSuggestionServiceTests
{
    [TestMethod]
    public void ExtractWordAtPosition_WhenCaretInsideWord_ReturnsWholeWord()
    {
        string? result = BookmarkNameSuggestionService.ExtractWordAtPosition("public void SaveDocumentAsync()", 13);

        Assert.AreEqual("SaveDocumentAsync", result);
    }

    [TestMethod]
    public void ExtractWordAtPosition_WhenCaretAtEndOfWord_ReturnsWholeWord()
    {
        string? result = BookmarkNameSuggestionService.ExtractWordAtPosition("SaveDocumentAsync", 17);

        Assert.AreEqual("SaveDocumentAsync", result);
    }

    [TestMethod]
    public void ExtractWordAtPosition_WhenCaretOnWhitespaceWithoutNeighborWord_ReturnsNull()
    {
        string? result = BookmarkNameSuggestionService.ExtractWordAtPosition("foo   bar", 4);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ExtractWordAtPosition_WhenWordIsCommonNoise_ReturnsNull()
    {
        string? result = BookmarkNameSuggestionService.ExtractWordAtPosition("for (int i = 0;", 9);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void NormalizeLabelCandidate_WhenTextContainsNewLine_UsesFirstLineOnly()
    {
        string? result = BookmarkNameSuggestionService.NormalizeLabelCandidate("  Primary Label  \r\nSecondary", 100);

        Assert.AreEqual("Primary Label", result);
    }

    [TestMethod]
    public void NormalizeLabelCandidate_WhenTextExceedsMaxLength_TrimsToLimit()
    {
        string? result = BookmarkNameSuggestionService.NormalizeLabelCandidate("0123456789", 5);

        Assert.AreEqual("01234", result);
    }

    [TestMethod]
    public void SelectBestIdentifier_PrefersDeclaredMemberOverGenericType()
    {
        // public List<DrawerData> DrawerData { get; set; }
        (string, string)[] spans =
        [
            ("keyword", "public"),
            ("class name", "List"),
            ("class name", "DrawerData"),
            ("property name", "DrawerData"),
            ("keyword", "get"),
            ("keyword", "set"),
        ];

        string? result = BookmarkNameSuggestionService.SelectBestIdentifier(spans);

        Assert.AreEqual("DrawerData", result);
    }

    [TestMethod]
    public void SelectBestIdentifier_PrefersFieldNameOverType()
    {
        // private readonly List<int> _items;
        (string, string)[] spans =
        [
            ("keyword", "private"),
            ("keyword", "readonly"),
            ("class name", "List"),
            ("keyword", "int"),
            ("field name", "_items"),
        ];

        string? result = BookmarkNameSuggestionService.SelectBestIdentifier(spans);

        Assert.AreEqual("_items", result);
    }

    [TestMethod]
    public void SelectBestIdentifier_DefinitionKeywordStillWins()
    {
        // class DrawerData
        (string, string)[] spans =
        [
            ("keyword", "class"),
            ("class name", "DrawerData"),
        ];

        string? result = BookmarkNameSuggestionService.SelectBestIdentifier(spans);

        Assert.AreEqual("DrawerData", result);
    }

    [TestMethod]
    public void SelectBestIdentifier_FallsBackToTypeWhenNoMember()
    {
        (string, string)[] spans =
        [
            ("keyword", "new"),
            ("class name", "List"),
        ];

        string? result = BookmarkNameSuggestionService.SelectBestIdentifier(spans);

        Assert.AreEqual("List", result);
    }
}
