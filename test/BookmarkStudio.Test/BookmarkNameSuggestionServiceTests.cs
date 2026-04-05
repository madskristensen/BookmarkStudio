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
}
