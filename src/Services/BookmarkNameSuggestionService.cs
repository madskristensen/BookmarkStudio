using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Tagging;

namespace BookmarkStudio
{
    /// <summary>
    /// Service that suggests meaningful bookmark names by analyzing code classifications on the current line.
    /// Uses the VS editor's classification system to identify identifiers, method names, class names, etc.
    /// </summary>
    [Export(typeof(BookmarkNameSuggestionService))]
    internal sealed class BookmarkNameSuggestionService
    {
        // Keywords that typically precede definition names (the identifier after these is what we want)
        private static readonly HashSet<string> DefinitionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // C#/VB type definitions
            "class", "struct", "interface", "enum", "record", "namespace",
            // Method/property modifiers that precede return type or name
            "void", "async", "override", "virtual", "abstract", "static",
            // C++ specific
            "typename", "template",
            // Property accessors
            "get", "set",
            // JavaScript/TypeScript
            "function", "const", "let", "var",
            // Python
            "def",
        };

        // Classification names that indicate meaningful identifiers
        private static readonly HashSet<string> IdentifierClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "identifier",
            "class name",
            "struct name",
            "interface name",
            "enum name",
            "method name",
            "function name",
            "property name",
            "field name",
            "local name",
            "parameter name",
            "type parameter name",
            "namespace name",
            // TypeScript/JavaScript
            "ts identifier",
            "js identifier",
        };

        // Classification names for keywords
        private static readonly HashSet<string> KeywordClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "keyword",
            "keyword - control",
            "ts keyword",
            "js keyword",
        };

        // Classification names for types (higher priority identifiers)
        private static readonly HashSet<string> TypeClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "class name",
            "struct name",
            "interface name",
            "enum name",
            "delegate name",
            "type parameter name",
        };

        [Import]
        private IViewClassifierAggregatorService ViewClassifierService { get; set; } = null!;

        [Import]
        private IClassifierAggregatorService BufferClassifierService { get; set; } = null!;

        [Import]
        private IViewTagAggregatorFactoryService TagAggregatorFactory { get; set; } = null!;

        /// <summary>
        /// Gets the singleton instance of the service via MEF composition.
        /// </summary>
        public static BookmarkNameSuggestionService? TryGetInstance()
        {
            try
            {
                var componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
                return componentModel?.GetService<BookmarkNameSuggestionService>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets selected text when the active editor has exactly one non-empty selection span.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The normalized selected text, or null when the active selection is not a single span.</returns>
        public async Task<string?> GetSingleSelectionSpanTextAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                DocumentView? docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextView is null)
                {
                    return null;
                }

                return GetSingleSelectionSpanTextFromTextView(docView.TextView);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                return null;
            }
        }

        /// <summary>
        /// Gets the word under the caret from the active editor.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The word under the caret, or null when no valid word can be resolved.</returns>
        public async Task<string?> GetWordUnderCaretAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                DocumentView? docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextView is null)
                {
                    return null;
                }

                return GetWordUnderCaretFromTextView(docView.TextView);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                return null;
            }
        }

        /// <summary>
        /// Suggests a meaningful name for a bookmark based on the code at the current caret position.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A suggested name, or null if no suitable name could be determined.</returns>
        public async Task<string?> GetSuggestedNameAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                DocumentView? docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextView is null)
                {
                    return null;
                }

                IWpfTextView textView = docView.TextView;
                return GetSuggestedNameFromTextView(textView);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                return null;
            }
        }

        /// <summary>
        /// Analyzes the line at the caret position and returns a suggested name.
        /// Uses the view classifier first, then falls back to buffer classifiers and tag aggregator.
        /// </summary>
        private string? GetSuggestedNameFromTextView(IWpfTextView textView)
        {
            if (textView.IsClosed || textView.TextSnapshot.Length == 0)
            {
                return null;
            }

            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;
            ITextSnapshotLine line = caretPosition.GetContainingLine();
            SnapshotSpan lineSpan = line.Extent;

            // Try the view classifier first (handles projection buffers)
            IClassifier viewClassifier = ViewClassifierService.GetClassifier(textView);
            IList<ClassificationSpan> classifications = viewClassifier.GetClassificationSpans(lineSpan);

            // If no classifications from view classifier, try buffer classifiers for projection buffers
            if (classifications.Count == 0 && textView.TextBuffer is IProjectionBuffer projection)
            {
                classifications = GetClassificationsFromProjectionBuffers(textView, projection, line);
            }

            // If still no classifications, try the tag aggregator
            if (classifications.Count == 0)
            {
                classifications = GetClassificationsFromTagAggregator(textView, lineSpan);
            }

            if (classifications.Count == 0)
            {
                return null;
            }

            return ExtractBestIdentifier(classifications);
        }

        private static string? GetSingleSelectionSpanTextFromTextView(IWpfTextView textView)
        {
            if (textView.IsClosed)
            {
                return null;
            }

            NormalizedSnapshotSpanCollection selectedSpans = textView.Selection.SelectedSpans;
            if (selectedSpans.Count != 1)
            {
                return null;
            }

            return NormalizeLabelCandidate(selectedSpans[0].GetText(), 100);
        }

        private static string? GetWordUnderCaretFromTextView(IWpfTextView textView)
        {
            if (textView.IsClosed || textView.TextSnapshot.Length == 0)
            {
                return null;
            }

            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;
            ITextSnapshotLine line = caretPosition.GetContainingLine();
            string lineText = line.GetText();
            int caretIndex = caretPosition.Position - line.Start.Position;

            return ExtractWordAtPosition(lineText, caretIndex);
        }

        /// <summary>
        /// Gets classifications by mapping the line to source buffers in a projection buffer.
        /// </summary>
        private IList<ClassificationSpan> GetClassificationsFromProjectionBuffers(
            IWpfTextView textView,
            IProjectionBuffer projection,
            ITextSnapshotLine line)
        {
            foreach (ITextBuffer sourceBuffer in projection.SourceBuffers)
            {
                if (sourceBuffer.CurrentSnapshot.Length == 0)
                {
                    continue;
                }

                // Map the line start to the source buffer
                SnapshotPoint? mappedStart = textView.BufferGraph.MapDownToBuffer(
                    line.Start,
                    PointTrackingMode.Negative,
                    sourceBuffer,
                    PositionAffinity.Predecessor);

                if (!mappedStart.HasValue)
                {
                    continue;
                }

                // Get the mapped line in the source buffer
                ITextSnapshotLine mappedLine = mappedStart.Value.GetContainingLine();
                SnapshotSpan mappedLineSpan = mappedLine.Extent;

                IClassifier bufferClassifier = BufferClassifierService.GetClassifier(sourceBuffer);
                IList<ClassificationSpan> classifications = bufferClassifier.GetClassificationSpans(mappedLineSpan);

                if (classifications.Count > 0)
                {
                    return classifications;
                }
            }

            return Array.Empty<ClassificationSpan>();
        }

        /// <summary>
        /// Gets classifications using the tag aggregator as a fallback.
        /// </summary>
        private IList<ClassificationSpan> GetClassificationsFromTagAggregator(IWpfTextView textView, SnapshotSpan lineSpan)
        {
            var result = new List<ClassificationSpan>();

            using (ITagAggregator<ClassificationTag> tagAggregator = TagAggregatorFactory.CreateTagAggregator<ClassificationTag>(textView))
            {
                var spans = new NormalizedSnapshotSpanCollection(lineSpan);

                foreach (IMappingTagSpan<ClassificationTag> tagSpan in tagAggregator.GetTags(spans))
                {
                    NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(lineSpan.Snapshot);
                    foreach (SnapshotSpan span in tagSpans)
                    {
                        result.Add(new ClassificationSpan(span, tagSpan.Tag.ClassificationType));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts the best identifier from the classifications on a line.
        /// Priority: identifier after definition keyword > type name > first identifier
        /// </summary>
        private static string? ExtractBestIdentifier(IList<ClassificationSpan> classifications)
        {
            string? identifierAfterKeyword = null;
            string? typeName = null;
            string? firstIdentifier = null;
            bool previousWasDefinitionKeyword = false;

            foreach (ClassificationSpan span in classifications)
            {
                string classificationType = span.ClassificationType.Classification;
                string text = span.Span.GetText().Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                // Check if this is a keyword
                if (IsKeywordClassification(classificationType))
                {
                    previousWasDefinitionKeyword = DefinitionKeywords.Contains(text);
                    continue;
                }

                // Check if this is an identifier
                if (IsIdentifierClassification(classificationType))
                {
                    // Skip very short identifiers or common noise
                    if (text.Length < 2 || IsCommonNoise(text))
                    {
                        previousWasDefinitionKeyword = false;
                        continue;
                    }

                    // Identifier right after a definition keyword - highest priority
                    if (previousWasDefinitionKeyword && identifierAfterKeyword is null)
                    {
                        identifierAfterKeyword = text;
                    }

                    // Type name - second priority
                    if (typeName is null && IsTypeClassification(classificationType))
                    {
                        typeName = text;
                    }

                    // First identifier - fallback
                    if (firstIdentifier is null)
                    {
                        firstIdentifier = text;
                    }

                    previousWasDefinitionKeyword = false;
                }
                else
                {
                    previousWasDefinitionKeyword = false;
                }
            }

            // Return in priority order
            return identifierAfterKeyword ?? typeName ?? firstIdentifier;
        }

        private static bool IsKeywordClassification(string classification)
            => KeywordClassifications.Any(k => classification.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsIdentifierClassification(string classification)
            => IdentifierClassifications.Any(i => classification.IndexOf(i, StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsTypeClassification(string classification)
            => TypeClassifications.Any(t => classification.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Returns true for common identifiers that don't make good bookmark names.
        /// </summary>
        private static bool IsCommonNoise(string text)
        {
            // Skip common single-letter variables, common abbreviations
            return text.Length == 1 ||
                   text.Equals("i", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("j", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("k", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("x", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("s", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("e", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("ex", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("it", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("_", StringComparison.Ordinal);
        }

        internal static string? ExtractWordAtPosition(string? lineText, int caretIndex)
        {
            if (lineText is null || string.IsNullOrWhiteSpace(lineText) || caretIndex < 0 || caretIndex > lineText.Length)
            {
                return null;
            }

            int index = caretIndex;
            if (index == lineText.Length)
            {
                index--;
            }

            if (index < 0)
            {
                return null;
            }

            if (!IsWordCharacter(lineText[index]))
            {
                if (index == 0 || !IsWordCharacter(lineText[index - 1]))
                {
                    return null;
                }

                index--;
            }

            int start = index;
            while (start > 0 && IsWordCharacter(lineText[start - 1]))
            {
                start--;
            }

            int end = index;
            while (end + 1 < lineText.Length && IsWordCharacter(lineText[end + 1]))
            {
                end++;
            }

            string? candidate = NormalizeLabelCandidate(lineText.Substring(start, end - start + 1), 100);
            if (candidate is null || IsCommonNoise(candidate))
            {
                return null;
            }

            return candidate;
        }

        internal static string? NormalizeLabelCandidate(string? text, int maxLength)
        {
            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string trimmed = text.Trim();
            int newlineIndex = trimmed.IndexOfAny(['\r', '\n']);
            if (newlineIndex >= 0)
            {
                trimmed = trimmed.Substring(0, newlineIndex).Trim();
            }

            if (trimmed.Length > maxLength)
            {
                trimmed = trimmed.Substring(0, maxLength).Trim();
            }

            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static bool IsWordCharacter(char c)
            => char.IsLetterOrDigit(c) || c == '_';
    }
}
