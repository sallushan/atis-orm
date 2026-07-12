using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Atis.Orm.Services
{
    /// <summary>
    ///     <para>
    ///         A thread-safe cache whose items expire after a period of inactivity (sliding
    ///         expiration: every successful <see cref="TryGetItem"/> refreshes the item's last access
    ///         time). A background timer periodically evicts expired items so a long-lived cache does
    ///         not pin values that are no longer being used.
    ///     </para>
    ///     <para>
    ///         Dispose the instance to stop the cleanup timer.
    ///     </para>
    /// </summary>
    /// <typeparam name="TKey">The type of the cache keys.</typeparam>
    /// <typeparam name="TValue">The type of the cached values.</typeparam>
    public class TimedCacheManager<TKey, TValue> : IDisposable
    {
        private class CacheItem
        {
            public TValue Value { get; set; }
            public DateTime LastAccessTime { get; set; }
        }

        private readonly ConcurrentDictionary<TKey, CacheItem> cache;
        private readonly TimeSpan timeout;
        private readonly Timer cleanupTimer;

        /// <summary>
        ///     Creates the cache with the given inactivity <paramref name="timeout"/>; the cleanup
        ///     timer runs on the same interval.
        /// </summary>
        /// <param name="timeout">How long an item stays valid after its last access.</param>
        public TimedCacheManager(TimeSpan timeout)
        {
            this.timeout = timeout;
            cache = new ConcurrentDictionary<TKey, CacheItem>();

            // Setting up a timer to periodically clear expired items
            cleanupTimer = new Timer(CleanupExpiredItems, null, timeout, timeout);
        }

        /// <summary>
        ///     Adds or replaces the item under <paramref name="key"/>, resetting its access time.
        /// </summary>
        public void SetItem(TKey key, TValue value)
        {
            var cacheItem = new CacheItem { Value = value, LastAccessTime = DateTime.UtcNow };
            cache[key] = cacheItem;
        }

        /// <summary>
        ///     Gets the item under <paramref name="key"/> if present and not expired, refreshing its
        ///     access time on a hit.
        /// </summary>
        /// <returns><c>true</c> when a non-expired item was found; otherwise <c>false</c>.</returns>
        public bool TryGetItem(TKey key, out TValue value)
        {
            if (cache.TryGetValue(key, out CacheItem item))
            {
                if ((DateTime.UtcNow - item.LastAccessTime) < timeout)
                {
                    item.LastAccessTime = DateTime.UtcNow;
                    value = item.Value;
                    return true;
                }
            }

            value = default; // Set default value if not found or expired
            return false; // Return false if not found or expired
        }

        private void CleanupExpiredItems(object state)
        {
            if (isDisposed) return;

            // Disable the timer to prevent re-entrancy
            cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);

            try
            {
                var keysToRemove = cache.Where(pair => (DateTime.UtcNow - pair.Value.LastAccessTime) >= timeout)
                                            .Select(pair => pair.Key)
                                            .ToList();

                foreach (var key in keysToRemove)
                {
                    cache.TryRemove(key, out _);
                }
                // Re-enable the timer
            }
            finally
            {
                try
                {
                    if (!isDisposed)
                        cleanupTimer.Change(timeout, timeout);
                }
                catch (ObjectDisposedException)
                {
                    // Dispose raced with the cleanup; the timer is gone, nothing to re-enable.
                }
            }
        }

        /// <summary>
        ///     Removes all items from the cache.
        /// </summary>
        public void Clear()
        {
            cache.Clear();
        }

        private bool isDisposed = false;
        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            cleanupTimer.Dispose();
        }
    }
}
