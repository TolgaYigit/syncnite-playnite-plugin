using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace PlayniteCloudSync.Tests
{
    public class CloudSyncApiClientTests
    {
        private class SentPushPayload
        {
            public List<PushGame> games { get; set; }
            public string plugin_version { get; set; }
        }

        [Fact]
        public async Task PushGamesAsync_SendsGamesAndPluginVersion_ReturnsUpsertedCount()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"upserted\": 3}");
            var client = new CloudSyncApiClient("https://example.com", "token-123", handler);
            var games = new List<PushGame>
            {
                new PushGame { PlayniteId = "a", Name = "Game A" },
                new PushGame { PlayniteId = "b", Name = "Game B" },
            };

            var upserted = await client.PushGamesAsync(games, "0.3.2");

            Assert.Equal(3, upserted);
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            Assert.Equal("https://example.com/api/sync/push", handler.LastRequest.RequestUri.ToString());
            Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization.Scheme);
            Assert.Equal("token-123", handler.LastRequest.Headers.Authorization.Parameter);

            var sent = JsonConvert.DeserializeObject<SentPushPayload>(handler.LastRequestBody);
            Assert.Equal("0.3.2", sent.plugin_version);
            Assert.Equal(2, sent.games.Count);
            Assert.Equal("a", sent.games[0].PlayniteId);
            Assert.Equal("b", sent.games[1].PlayniteId);
        }

        // This is the exact bug that started all of this: a large push used to come back as a
        // bare HTTP 413 with no JSON body, and the client needs to surface something readable
        // either way - with a body (this test) and without one (next test).
        [Fact]
        public async Task PushGamesAsync_ServerReturnsJsonError_ThrowsWithThatMessage()
        {
            var handler = new FakeHttpMessageHandler((HttpStatusCode)413, "{\"error\": \"Payload too large\"}");
            var client = new CloudSyncApiClient("https://example.com", "token", handler);

            var ex = await Assert.ThrowsAsync<CloudSyncApiException>(
                () => client.PushGamesAsync(new List<PushGame>(), "0.3.2"));

            Assert.Equal("Payload too large", ex.Message);
        }

        [Fact]
        public async Task PushGamesAsync_ServerReturnsNonJsonBody_FallsBackToHttpStatus()
        {
            var handler = new FakeHttpMessageHandler((HttpStatusCode)413, "Request Entity Too Large");
            var client = new CloudSyncApiClient("https://example.com", "token", handler);

            var ex = await Assert.ThrowsAsync<CloudSyncApiException>(
                () => client.PushGamesAsync(new List<PushGame>(), "0.3.2"));

            Assert.Equal("HTTP 413", ex.Message);
        }

        [Fact]
        public async Task RedeemPairingCodeAsync_ReturnsToken()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"token\": \"device-token-abc\"}");
            var client = new CloudSyncApiClient("https://example.com", handler: handler);

            var token = await client.RedeemPairingCodeAsync("ABC123");

            Assert.Equal("device-token-abc", token);
            var sentPayload = JsonConvert.DeserializeObject<Dictionary<string, string>>(handler.LastRequestBody);
            Assert.Equal("ABC123", sentPayload["code"]);
        }

        [Fact]
        public async Task RedeemPairingCodeAsync_InvalidCode_Throws()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, "{\"error\": \"Invalid or expired code\"}");
            var client = new CloudSyncApiClient("https://example.com", handler: handler);

            var ex = await Assert.ThrowsAsync<CloudSyncApiException>(
                () => client.RedeemPairingCodeAsync("EXPIRED"));

            Assert.Equal("Invalid or expired code", ex.Message);
        }

        [Fact]
        public async Task PullGamesAsync_ParsesGamesAndServerTime()
        {
            var body = "{\"games\": [{\"playnite_id\": \"a\", \"tags\": [\"x\"]}], \"server_time\": \"2026-01-01T00:00:00Z\"}";
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, body);
            var client = new CloudSyncApiClient("https://example.com", handler: handler);

            var result = await client.PullGamesAsync(null);

            Assert.Single(result.Games);
            Assert.Equal("a", result.Games[0].PlayniteId);
            Assert.Equal("2026-01-01T00:00:00Z", result.ServerTime);
            Assert.DoesNotContain("since=", handler.LastRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task PullGamesAsync_WithSince_AppendsQueryParam()
        {
            var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"games\": [], \"server_time\": \"now\"}");
            var client = new CloudSyncApiClient("https://example.com", handler: handler);

            var since = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await client.PullGamesAsync(since);

            Assert.Contains("since=", handler.LastRequest.RequestUri.ToString());
        }
    }
}
