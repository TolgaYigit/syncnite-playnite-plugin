using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PlayniteCloudSync
{
    public class PushGame
    {
        [JsonProperty("playnite_id")]
        public string PlayniteId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("install_status")]
        public string InstallStatus { get; set; }

        [JsonProperty("playtime_minutes")]
        public long PlaytimeMinutes { get; set; }

        [JsonProperty("last_played")]
        public string LastPlayed { get; set; }

        [JsonProperty("achievements_unlocked")]
        public int? AchievementsUnlocked { get; set; }

        [JsonProperty("achievements_total")]
        public int? AchievementsTotal { get; set; }
    }

    public class PullGame
    {
        [JsonProperty("playnite_id")]
        public string PlayniteId { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("categories")]
        public List<string> Categories { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        [JsonProperty("completion_status")]
        public string CompletionStatus { get; set; }

        [JsonProperty("favorite")]
        public bool Favorite { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }
    }

    public class PullResult
    {
        [JsonProperty("games")]
        public List<PullGame> Games { get; set; }

        [JsonProperty("server_time")]
        public string ServerTime { get; set; }
    }

    public class CloudSyncApiClient
    {
        private readonly string apiBaseUrl;
        private readonly string deviceToken;
        private readonly HttpClient http = new HttpClient();

        public CloudSyncApiClient(string apiBaseUrl, string deviceToken = null)
        {
            this.apiBaseUrl = apiBaseUrl.TrimEnd('/');
            this.deviceToken = deviceToken;
        }

        public async Task<string> RedeemPairingCodeAsync(string code)
        {
            var payload = JsonConvert.SerializeObject(new { code, device_name = Environment.MachineName });
            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(apiBaseUrl + "/api/pairing/redeem", content))
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new CloudSyncApiException(ExtractError(body, response.StatusCode));
                }

                var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                return result["token"];
            }
        }

        public async Task<int> PushGamesAsync(IEnumerable<PushGame> games, CancellationToken ct = default(CancellationToken))
        {
            var payload = JsonConvert.SerializeObject(new { games });
            using (var request = new HttpRequestMessage(HttpMethod.Post, apiBaseUrl + "/api/sync/push"))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using (var response = await http.SendAsync(request, ct))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new CloudSyncApiException(ExtractError(body, response.StatusCode));
                    }

                    var result = JsonConvert.DeserializeObject<Dictionary<string, int>>(body);
                    return result.ContainsKey("upserted") ? result["upserted"] : 0;
                }
            }
        }

        public async Task<PullResult> PullGamesAsync(DateTime? since, CancellationToken ct = default(CancellationToken))
        {
            var url = apiBaseUrl + "/api/sync/pull";
            if (since.HasValue)
            {
                url += "?since=" + Uri.EscapeDataString(since.Value.ToString("o"));
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", deviceToken);

                using (var response = await http.SendAsync(request, ct))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new CloudSyncApiException(ExtractError(body, response.StatusCode));
                    }

                    return JsonConvert.DeserializeObject<PullResult>(body);
                }
            }
        }

        private static string ExtractError(string body, System.Net.HttpStatusCode status)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                if (parsed != null && parsed.ContainsKey("error"))
                {
                    return parsed["error"];
                }
            }
            catch (JsonException)
            {
                // fall through
            }

            return $"HTTP {(int)status}";
        }
    }

    public class CloudSyncApiException : Exception
    {
        public CloudSyncApiException(string message) : base(message)
        {
        }
    }
}
