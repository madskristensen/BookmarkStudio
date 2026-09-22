using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace BookmarkStudio
{
    [SolutionTreeFilterProvider(PackageGuids.BookmarkStudioString, PackageIds.SolutionExplorerBookmarkFilterCommand)]
    public sealed class SolutionExplorerBookmarkFilter : HierarchyTreeFilterProvider
    {
        private readonly IVsHierarchyItemCollectionProvider _hierarchyCollectionProvider;

        [ImportingConstructor]
        public SolutionExplorerBookmarkFilter(IVsHierarchyItemCollectionProvider hierarchyCollectionProvider)
        {
            _hierarchyCollectionProvider = hierarchyCollectionProvider;
        }

        protected override HierarchyTreeFilter CreateFilter()
            => new Filter(_hierarchyCollectionProvider);

        private sealed class Filter : HierarchyTreeFilter
        {
            private readonly IVsHierarchyItemCollectionProvider _hierarchyCollectionProvider;

            public Filter(IVsHierarchyItemCollectionProvider hierarchyCollectionProvider)
            {
                _hierarchyCollectionProvider = hierarchyCollectionProvider;
            }

            protected override async Task<IReadOnlyObservableSet> GetIncludedItemsAsync(IEnumerable<IVsHierarchyItem> rootItems)
            {
                IVsHierarchyItem root = HierarchyUtilities.FindCommonAncestor(rootItems);
                IReadOnlyObservableSet<IVsHierarchyItem> sourceItems = await _hierarchyCollectionProvider.GetDescendantsAsync(
                    root.HierarchyIdentity.NestedHierarchy,
                    CancellationToken);

                return new BookmarkedHierarchyItemSet(sourceItems);
            }
        }

        private sealed class BookmarkedHierarchyItemSet : IFilteredHierarchyItemSet
        {
            private readonly IReadOnlyObservableSet<IVsHierarchyItem> _sourceItems;
            private bool _isDisposed;

            public BookmarkedHierarchyItemSet(IReadOnlyObservableSet<IVsHierarchyItem> sourceItems)
            {
                _sourceItems = sourceItems;
                _sourceItems.CollectionChanged += OnCollectionChanged;
                BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
            }

            public int Count => this.Count(IsIncluded);

            public event NotifyCollectionChangedEventHandler? CollectionChanged;

            public bool Contains(IVsHierarchyItem item)
                => IsIncluded(item);

            public bool Contains(object item)
                => item is IVsHierarchyItem hierarchyItem && IsIncluded(hierarchyItem);

            public IEnumerator<IVsHierarchyItem> GetEnumerator()
                => _sourceItems.Where(IsIncluded).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _sourceItems.CollectionChanged -= OnCollectionChanged;
                BookmarkStudioSession.Current.BookmarksChanged -= OnBookmarksChanged;
            }

            private static bool IsIncluded(IVsHierarchyItem item)
            {
                return item is not null
                    && !item.IsDisposed
                    && HierarchyUtilities.IsPhysicalFile(item.HierarchyIdentity)
                    && BookmarkIdentity.IsBookmarkedDocumentPath(item.CanonicalName, BookmarkStudioSession.Current.CachedBookmarks);
            }

            private void OnBookmarksChanged(object sender, EventArgs e)
                => RaiseReset();

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
                => RaiseReset();

            private void RaiseReset()
            {
                if (!_isDisposed)
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }
    }
}
