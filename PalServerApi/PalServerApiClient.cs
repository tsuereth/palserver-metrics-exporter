using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    public class PalServerApiClient : IDisposable
    {
        // The PalWorld server REST API requires HTTP Basic authorization.
        // The username is always "admin" ...
        // And the password is configurable in server settings.
        private const string AdminUsername = "admin";

        private readonly ILogger logger;

        private HttpClient httpClient;
        private bool disposed;

        private string baseUrl;
        private string encodedUserPassString;

        public PalServerApiClient(
            ILogger logger,
            string targetHost,
            UInt16 targetPort,
            string targetAdminPassword)
        {
            if (string.IsNullOrEmpty(targetHost))
            {
                throw new ArgumentException("Missing or empty target server host");
            }
            if (targetPort == 0)
            {
                throw new ArgumentException("Invalid target server port");
            }

            this.logger = logger;

            this.httpClient = new HttpClient();
            this.disposed = false;

            this.baseUrl = $"http://{targetHost}:{targetPort}/v1/api/";

            var userPassString = $"{AdminUsername}:{targetAdminPassword}";
            this.encodedUserPassString = Convert.ToBase64String(Encoding.UTF8.GetBytes(userPassString));
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    this.httpClient.Dispose();

                    this.disposed = true;
                }
            }
        }

        private async Task<T> GetApiResponseObjectAsync<T>(string apiEndpoint)
        {
            var requestUri = baseUrl + apiEndpoint;

            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Authorization", $"Basic {this.encodedUserPassString}");

                var response = await this.httpClient.SendAsync(request);
                var responseText = await response.Content.ReadAsStringAsync();

                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex) when (!string.IsNullOrEmpty(responseText))
                {
                    throw new AggregateException(responseText, ex);
                }

                var responseObject = JsonConvert.DeserializeObject<T>(responseText);
                return responseObject;
            }
        }

        public async Task<PalServerInfo> GetInfoAsync()
        {
            return await this.GetApiResponseObjectAsync<PalServerInfo>("info");
        }

        public async Task<PalServerMetrics> GetMetricsAsync()
        {
            return await this.GetApiResponseObjectAsync<PalServerMetrics>("metrics");
        }

        public async Task<PalServerPlayers> GetPlayersAsync()
        {
            return await this.GetApiResponseObjectAsync<PalServerPlayers>("players");
        }

        public async Task<PalServerSettings> GetSettingsAsync()
        {
            return await this.GetApiResponseObjectAsync<PalServerSettings>("settings");
        }
    }
}
