using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteCloudSync.Tests
{
    // Captures the single most recent request for assertions, and returns a canned response -
    // enough for CloudSyncApiClient's tests without pulling in a mocking library.
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;

        public HttpRequestMessage LastRequest { get; private set; }
        public string LastRequestBody { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content != null
                ? await request.Content.ReadAsStringAsync()
                : null;

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody ?? string.Empty),
            };
        }
    }
}
