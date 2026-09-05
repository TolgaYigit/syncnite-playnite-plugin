using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PlayniteCloudSync.Tests
{
    public class BatchingTests
    {
        [Fact]
        public void EmptyList_ProducesNoChunks()
        {
            var result = Batching.Chunk(new List<int>(), 10).ToList();
            Assert.Empty(result);
        }

        [Fact]
        public void ExactMultiple_SplitsEvenly()
        {
            var items = Enumerable.Range(1, 10).ToList();
            var chunks = Batching.Chunk(items, 5).ToList();

            Assert.Equal(2, chunks.Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, chunks[0]);
            Assert.Equal(new[] { 6, 7, 8, 9, 10 }, chunks[1]);
        }

        [Fact]
        public void Remainder_EndsInASmallerFinalChunk()
        {
            var items = Enumerable.Range(1, 7).ToList();
            var chunks = Batching.Chunk(items, 3).ToList();

            Assert.Equal(3, chunks.Count);
            Assert.Equal(3, chunks[0].Count);
            Assert.Equal(3, chunks[1].Count);
            Assert.Single(chunks[2]);
            Assert.Equal(7, chunks[2][0]);
        }

        [Fact]
        public void ChunkSizeLargerThanList_ProducesOneChunkWithEverything()
        {
            var items = Enumerable.Range(1, 3).ToList();
            var chunks = Batching.Chunk(items, 2000).ToList();

            Assert.Single(chunks);
            Assert.Equal(items, chunks[0]);
        }

        [Fact]
        public void SingleItemChunks_PreserveOrder()
        {
            var items = new List<string> { "a", "b", "c" };
            var chunks = Batching.Chunk(items, 1).ToList();

            Assert.Equal(3, chunks.Count);
            Assert.Equal("a", chunks[0][0]);
            Assert.Equal("b", chunks[1][0]);
            Assert.Equal("c", chunks[2][0]);
        }

        [Fact]
        public void ZeroChunkSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Batching.Chunk(new List<int> { 1 }, 0).ToList());
        }

        [Fact]
        public void NegativeChunkSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Batching.Chunk(new List<int> { 1 }, -1).ToList());
        }

        [Fact]
        public void NullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Batching.Chunk<int>(null, 10).ToList());
        }

        // The actual scenario that started all of this: a five-digit library, chunked at the
        // real PushChunkSize used in PlayniteCloudSyncPlugin.SyncNowAsync. If that constant
        // ever changes without updating this test, that's the point - it's meant to fail loudly.
        [Fact]
        public void LargeLibrary_ChunksAtRealPushSize()
        {
            var items = Enumerable.Range(1, 47_000).ToList();
            var chunks = Batching.Chunk(items, 2000).ToList();

            Assert.Equal(24, chunks.Count); // 23 full chunks of 2000 + 1 of 1000
            Assert.All(chunks.Take(23), c => Assert.Equal(2000, c.Count));
            Assert.Equal(1000, chunks.Last().Count);
            Assert.Equal(47_000, chunks.Sum(c => c.Count));
        }
    }
}
