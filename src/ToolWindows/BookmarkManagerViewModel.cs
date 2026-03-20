using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Data;

namespace BookmarkStudio
{
    internal sealed class BookmarkManagerViewModel : INotifyPropertyChanged
    {
        private readonly BookmarkOperationsService _operations = BookmarkOperationsService.Current;
        private readonly ICollectionView _bookmarksView;
        private ManagedBookmark? _selectedBookmark;
        private string _searchText = string.Empty;
        private int? _selectedSlotNumber;
        private string _selectedGroupText = string.Empty;
        private string _selectedLabelText = string.Empty;
        private string _statusText = "Loading bookmarks...";
        private string _columnSortProperty;
        private ListSortDirection _columnSortDirection = ListSortDirection.Ascending;

        public BookmarkManagerViewModel()
        {
            _bookmarksView = CollectionViewSource.GetDefaultView(Bookmarks);
            _bookmarksView.Filter = FilterBookmark;
            RefreshView();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ManagedBookmark> Bookmarks { get; } = new ObservableCollection<ManagedBookmark>();

        public ICollectionView BookmarksView => _bookmarksView;

        public IReadOnlyList<int> SlotOptions { get; } = Enumerable.Range(1, 9).ToArray();

        internal string ColumnSortProperty => _columnSortProperty;

        internal ListSortDirection ColumnSortDirection => _columnSortDirection;

        public ManagedBookmark? SelectedBookmark
        {
            get => _selectedBookmark;
            set
            {
                if (ReferenceEquals(_selectedBookmark, value))
                {
                    return;
                }

                _selectedBookmark = value;
                SelectedLabelText = value?.Label ?? string.Empty;
                SelectedGroupText = value?.Group ?? string.Empty;
                SelectedSlotNumber = value?.SlotNumber;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedBookmark));
                OnPropertyChanged(nameof(SelectedLocation));
                OnPropertyChanged(nameof(SelectedPreview));
            }
        }

        public bool HasSelectedBookmark => SelectedBookmark is not null;

        public string SelectedLocation => SelectedBookmark?.Location ?? string.Empty;

        public string SelectedPreview => SelectedBookmark?.LineText ?? string.Empty;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (string.Equals(_searchText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                _bookmarksView.Refresh();
            }
        }

        public string SelectedLabelText
        {
            get => _selectedLabelText;
            set
            {
                if (string.Equals(_selectedLabelText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedLabelText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string SelectedGroupText
        {
            get => _selectedGroupText;
            set
            {
                if (string.Equals(_selectedGroupText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedGroupText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public int? SelectedSlotNumber
        {
            get => _selectedSlotNumber;
            set
            {
                if (_selectedSlotNumber == value)
                {
                    return;
                }

                _selectedSlotNumber = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
            => RefreshAsync(cancellationToken);

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.RefreshAsync(cancellationToken);
            string? bookmarkId = SelectedBookmark?.BookmarkId;
            ReloadBookmarks(bookmarks, bookmarkId);
            int totalCount = Bookmarks.Count;
            int visibleCount = _bookmarksView.Cast<object>().Count();
            SetStatus(visibleCount == totalCount
                ? string.Concat(totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks loaded.")
                : string.Concat(
                    visibleCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    " of ",
                    totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    " bookmarks visible."));
        }

        public async Task SaveSelectionAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            await _operations.RenameLabelAsync(selectedBookmark.BookmarkId, SelectedLabelText, cancellationToken);
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.SetGroupAsync(selectedBookmark.BookmarkId, SelectedGroupText, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus("Bookmark metadata updated.");
        }

        public async Task AssignSelectedSlotAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            if (!SelectedSlotNumber.HasValue)
            {
                throw new InvalidOperationException("Select a slot before assigning it.");
            }

            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.AssignSlotAsync(SelectedSlotNumber.Value, selectedBookmark.BookmarkId, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus(string.Concat("Slot ", SelectedSlotNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), " assigned."));
        }

        public async Task AssignSelectedSlotAsync(int slotNumber, CancellationToken cancellationToken)
        {
            SelectedSlotNumber = slotNumber;
            await AssignSelectedSlotAsync(cancellationToken);
        }

        public async Task ClearSelectedSlotAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.ClearSlotAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus("Bookmark slot cleared.");
        }

        public async Task ClearSelectedGroupAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.ClearGroupAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus("Bookmark group cleared.");
        }

        public async Task SetSelectedColorAsync(BookmarkColor color, CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.SetColorAsync(selectedBookmark.BookmarkId, color, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus(string.Concat("Bookmark color set to ", color.ToString(), "."));
        }

        public async Task ClearSelectedColorAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.ClearColorAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadBookmarks(bookmarks, selectedBookmark.BookmarkId);
            SetStatus("Bookmark color cleared.");
        }

        public async Task DeleteSelectedAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.RemoveBookmarkAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadBookmarks(bookmarks, null);
            SetStatus("Bookmark removed.");
        }

        public Task NavigateSelectedAsync(CancellationToken cancellationToken)
            => _operations.NavigateToBookmarkAsync(GetRequiredSelection().BookmarkId, cancellationToken);

        public Task<string> GetSelectedLocationAsync(CancellationToken cancellationToken)
            => _operations.GetBookmarkLocationAsync(GetRequiredSelection().BookmarkId, cancellationToken);

        internal void SetStatus(string statusText)
            => StatusText = statusText ?? string.Empty;

        internal int ApplySearchText(string searchText)
        {
            SearchText = searchText;
            return _bookmarksView.Cast<object>().Count();
        }

        internal void Clear()
        {
            _columnSortProperty = null;
            _columnSortDirection = ListSortDirection.Ascending;
            Bookmarks.Clear();
            SelectedBookmark = null;
            SearchText = string.Empty;
            OnPropertyChanged(nameof(ColumnSortProperty));
            SetStatus("No solution loaded.");
        }

        internal void ClearSearch()
            => SearchText = string.Empty;

        internal void SortByColumn(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            if (string.Equals(_columnSortProperty, propertyName, StringComparison.Ordinal))
            {
                _columnSortDirection = _columnSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _columnSortProperty = propertyName;
                _columnSortDirection = ListSortDirection.Ascending;
            }

            OnPropertyChanged(nameof(ColumnSortProperty));
            RefreshView();
        }

        internal void SelectBookmark(string? bookmarkId)
        {
            SelectedBookmark = string.IsNullOrWhiteSpace(bookmarkId)
                ? null
                : Bookmarks.FirstOrDefault(item => string.Equals(item.BookmarkId, bookmarkId, StringComparison.Ordinal));
        }

        private void ReloadBookmarks(IReadOnlyList<ManagedBookmark> bookmarks, string? bookmarkId)
        {
            Bookmarks.Clear();

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                Bookmarks.Add(bookmark);
            }

            RefreshView();

            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                SelectedBookmark = Bookmarks.FirstOrDefault();
                return;
            }

            SelectBookmark(bookmarkId);
            if (SelectedBookmark is null)
            {
                SelectedBookmark = Bookmarks.FirstOrDefault();
            }
        }

        private ManagedBookmark GetRequiredSelection()
            => SelectedBookmark ?? throw new InvalidOperationException("Select a bookmark first.");

        private bool FilterBookmark(object item)
        {
            ManagedBookmark bookmark = (ManagedBookmark)item;
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            string search = SearchText.Trim();
            return Contains(bookmark.Label, search)
                || Contains(bookmark.Group, search)
                || Contains(bookmark.FileName, search)
                || Contains(bookmark.DocumentPath, search)
                || Contains(bookmark.LineText, search)
                || Contains(bookmark.Location, search)
                || Contains(bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture), search)
                || (bookmark.Color != BookmarkColor.None && Contains(bookmark.Color.ToString(), search));
        }

        private void RefreshView()
        {
            using (_bookmarksView.DeferRefresh())
            {
                _bookmarksView.SortDescriptions.Clear();
                _bookmarksView.GroupDescriptions.Clear();

                ApplySort();
            }
        }

        private void ApplySort()
        {
            if (!string.IsNullOrEmpty(_columnSortProperty))
            {
                _bookmarksView.SortDescriptions.Add(new SortDescription(_columnSortProperty, _columnSortDirection));
                return;
            }

            _bookmarksView.SortDescriptions.Add(new SortDescription(nameof(ManagedBookmark.SlotNumber), ListSortDirection.Ascending));
            _bookmarksView.SortDescriptions.Add(new SortDescription(nameof(ManagedBookmark.FileName), ListSortDirection.Ascending));
            _bookmarksView.SortDescriptions.Add(new SortDescription(nameof(ManagedBookmark.LineNumber), ListSortDirection.Ascending));
        }

        private static bool Contains(string? value, string search)
            => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}