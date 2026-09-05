using System;
using System.Collections.Generic;

namespace PlayniteCloudSync
{
    // Pulled out of PlayniteCloudSyncPlugin.SyncNowAsync so the chunking behavior itself -
    // exact multiples, remainders, chunk size bigger than the list, empty input - can be unit
    // tested without spinning up Playnite's own APIs (IPlayniteAPI, GlobalProgressActionArgs,
    // etc.), which SyncNowAsync is otherwise built entirely around.
    public static class Batching
    {
        public static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int chunkSize)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");
            }

            for (var offset = 0; offset < items.Count; offset += chunkSize)
            {
                var size = Math.Min(chunkSize, items.Count - offset);
                var chunk = new List<T>(size);
                for (var i = 0; i < size; i++)
                {
                    chunk.Add(items[offset + i]);
                }
                yield return chunk;
            }
        }
    }
}
