using System;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkRefreshMonitorService : IDisposable
    {
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        private static class BookmarkRefreshMonitorServiceHolder
        {
            internal static readonly BookmarkRefreshMonitorService Instance = new BookmarkRefreshMonitorService();
        }

        internal static BookmarkRefreshMonitorService Instance => BookmarkRefreshMonitorServiceHolder.Instance;
    }
}