using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PalServerMetricsExporter.PalServerApi;
using Prometheus;

namespace PalServerMetricsExporter
{
    public class PalServerPrometheusExporter : IDisposable
    {
        private readonly ILogger logger;
        private readonly PalServerApiClient apiClient;
        private readonly bool includePlayerData;
        private readonly bool ignoreZeroPingPlayers;
        private readonly bool includeServerSettings;
        private readonly bool includeGameData;
        private readonly TimeSpan updateInterval;
        private readonly MetricServer exporterServer;
        private readonly IManagedLifetimeMetricFactory metricFactory;

        private bool disposed;

        public PalServerPrometheusExporter(
            ILogger logger,
            string palServerHost,
            ushort palServerPort,
            string palServerAdminPassword,
            bool includePlayerData,
            bool ignoreZeroPingPlayers,
            bool includeServerSettings,
            bool includeGameData,
            TimeSpan updateInterval,
            string bindHost,
            ushort bindPort,
            string serveMetricsPath)
        {
            this.logger = logger;

            logger.LogInformation($"Prometheus Exporter configuring PalServer API client with target: http://{palServerHost}:{palServerPort}");
            this.apiClient = new PalServerApiClient(logger, palServerHost, palServerPort, palServerAdminPassword);
            this.includePlayerData = includePlayerData;
            this.ignoreZeroPingPlayers = ignoreZeroPingPlayers;
            this.includeServerSettings = includeServerSettings;
            this.includeGameData = includeGameData;

            this.updateInterval = updateInterval;

            // Quirk notes:
            // - .NET's HttpListener rejects the well-known "0.0.0.0" bind host address;
            //   it instead expects the hostname "*" when binding to all local hostnames/addrs.
            // - When Prometheus.MetricServer formats a "prefix" for its underlying HttpListener,
            //   it adds a leading '/'; so remove a leading '/' from our own argument.
            // - The underlying HttpListener requires a trailing '/' on its "prefix" parameter,
            //   even though that trailing '/' isn't a literal requirement for client requests.
            var normalizedBindHost = bindHost;
            if (normalizedBindHost.Equals("0.0.0.0", StringComparison.Ordinal))
            {
                normalizedBindHost = "*";
            }
            var normalizedMetricsPath = serveMetricsPath.TrimStart('/');
            if (!normalizedMetricsPath.EndsWith('/'))
            {
                normalizedMetricsPath += '/';
            }
            logger.LogInformation($"Prometheus Exporter serving metrics at: http://{bindHost}:{bindPort}{serveMetricsPath}");
            this.exporterServer = new Prometheus.MetricServer(hostname: normalizedBindHost, port: bindPort, url: normalizedMetricsPath);

            // By default, program metrics will include a ton of .NET diagnostics from _this_ application.
            Metrics.SuppressDefaultMetrics();
            // Configure the metrics factory to expire instrumentation if it hasn't been updated recently.
            // Here we're going to squint and say that "recent" is within two update intervals.
            this.metricFactory = Metrics.WithManagedLifetime(expiresAfter: updateInterval * 2);
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
                    this.apiClient.Dispose();

                    this.disposed = true;
                }
            }
        }

        public async Task ServeAsync(CancellationToken cancelToken)
        {
            var metricValues = new PalServerMetricsValues(
                this.metricFactory,
                this.includePlayerData,
                this.ignoreZeroPingPlayers,
                this.includeServerSettings,
                this.includeGameData);

            // Loop until canceled, periodically requesting the server's metrics
            // and updating the exported instrumentation accordingly.
            while (!cancelToken.IsCancellationRequested)
            {
                var updateTimer = Stopwatch.StartNew();
                try
                {
                    logger.LogDebug("Updating current metric values");

                    // Initiate all PalServer API requests, and wait for them in parallel.
                    var apiTasks = new List<Task>();

                    var infoTask = this.apiClient.GetInfoAsync(cancelToken);
                    apiTasks.Add(infoTask);

                    var metricsTask = this.apiClient.GetMetricsAsync(cancelToken);
                    apiTasks.Add(metricsTask);

                    Task<PalServerPlayers> playersTask = null;
                    if (includePlayerData)
                    {
                        playersTask = this.apiClient.GetPlayersAsync(cancelToken);
                        apiTasks.Add(playersTask);
                    }

                    Task<PalServerSettings> settingsTask = null;
                    if (includeServerSettings)
                    {
                        settingsTask = this.apiClient.GetSettingsAsync(cancelToken);
                        apiTasks.Add(settingsTask);
                    }

                    Task<PalServerGameData> gameDataTask = null;
                    if (includeGameData)
                    {
                        gameDataTask = this.apiClient.GetGameDataAsync(cancelToken);
                        apiTasks.Add(gameDataTask);
                    }

                    await Task.WhenAll(apiTasks);

                    metricValues.Update(
                        infoTask.Result,
                        metricsTask.Result,
                        playersTask?.Result,
                        settingsTask?.Result,
                        gameDataTask?.Result);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Metrics update failed");
                }

                updateTimer.Stop();
                var updateDuration = TimeSpan.FromMilliseconds(updateTimer.ElapsedMilliseconds);
                if (updateDuration >= this.updateInterval)
                {
                    this.logger.LogWarning($"Metrics update took {updateDuration.Milliseconds} ms, exceeding the delay interval {this.updateInterval.Milliseconds} ms; next update will start immediately");
                }
                else
                {
                    var timeToNextUpdate = this.updateInterval - updateDuration;
                    await Task.Delay(timeToNextUpdate, cancelToken);
                }
            }
        }
    }
}
