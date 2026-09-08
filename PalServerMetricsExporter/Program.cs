using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Options;
using PalServerMetricsExporter.PalServerApi;
using Prometheus;

namespace PalServerMetricsExporter
{
    internal sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(c => c.AddSystemdConsole());
            var logger = loggerFactory.CreateLogger<Program>();

            var appAssembly = Assembly.GetExecutingAssembly();
            var appVersion = appAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            var printHelp = false;
            var printVersion = false;

            var palServerHost = "localhost";
            var palServerPortString = "8212";
            var palServerAdminPassword = string.Empty;
            var palServerAdminPasswordFile = string.Empty;

            var includePlayerDataString = true.ToString();
            var ignoreZeroPingPlayersString = true.ToString();
            var includeServerSettingsString = true.ToString();

            var updateIntervalSecondsString = "15";

            var exportBindHost = "0.0.0.0";
            var exportBindPortString = "8213";
            var exportPath = "/metrics";

            var options = new OptionSet()
            {
                { "help", "Print help text", _ => printHelp = true },
                { "version", "Print application version", _ => printVersion = true },

                { "palserver-host=", $"Target PalServer host, default: {palServerHost}", o => palServerHost = o },
                { "palserver-port=", $"Target PalServer port, default: {palServerPortString}", o => palServerPortString = o },
                { "palserver-admin-password=", $"Target PalServer AdminPassword, default: {palServerAdminPassword}", o => palServerAdminPassword = o },
                { "palserver-admin-password-file=", $"Path to a file containing the target PalServer AdminPassword, default: {palServerAdminPasswordFile}", o => palServerAdminPasswordFile = o },

                { "include-player-data=", $"Include player data in exported metrics, default: {includePlayerDataString}", o => includePlayerDataString = o },
                { "ignore-zero-ping-players=", $"When reporting player data, ignore data with a ping of zero, default: {ignoreZeroPingPlayersString}", o => ignoreZeroPingPlayersString = o },
                { "include-server-settings=", $"Include server settings in exported metrics, default: {includeServerSettingsString}", o => includeServerSettingsString = o },

                { "update-interval-seconds", $"Interval (in seconds) between requesting updates from the target PalServer, default: {updateIntervalSecondsString}", o => updateIntervalSecondsString = o },

                { "export-bind-host=", $"Hostname or IP address on which to serve metrics, default: {exportBindHost}", o => exportBindHost = o },
                { "export-bind-port=", $"TCP port on which to serve metrics, default: {exportBindPortString}", o => exportBindPortString = o },
                { "export-path=", $"HTTP path at which to serve metrics, default: {exportPath}", o => exportPath = o },
            };
            options.Parse(args);

            if (printHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return 0;
            }
            if (printVersion)
            {
                logger.LogInformation($"{appAssembly.GetName().Name} version {appVersion}");
                return 0;
            }

            ushort palServerPort;
            if (!ushort.TryParse(palServerPortString, out palServerPort))
            {
                throw new ArgumentException($"Failed to parse ushort from palserver-port argument '{palServerPortString}'");
            }
            if (!string.IsNullOrEmpty(palServerAdminPassword) && !string.IsNullOrEmpty(palServerAdminPasswordFile))
            {
                throw new ArgumentException($"Cannot specify both palserver-admin-password and palserver-admin-password-file");
            }
            if (!string.IsNullOrEmpty(palServerAdminPasswordFile))
            {
                logger.LogInformation($"Reading PalServer AdminPassword from file: {palServerAdminPasswordFile}");
                palServerAdminPassword = File.ReadAllText(palServerAdminPasswordFile).Trim();
            }

            bool includePlayerData;
            if (!bool.TryParse(includePlayerDataString, out includePlayerData))
            {
                throw new ArgumentException($"Failed to parse bool from include-server-data argument '{includePlayerDataString}'");
            }
            bool ignoreZeroPingPlayers;
            if (!bool.TryParse(ignoreZeroPingPlayersString, out ignoreZeroPingPlayers))
            {
                throw new ArgumentException($"Failed to parse bool from ignore-zero-ping-players argument '{ignoreZeroPingPlayersString}'");
            }
            bool includeServerSettings;
            if (!bool.TryParse(includeServerSettingsString, out includeServerSettings))
            {
                throw new ArgumentException($"Failed to parse bool from include-server-settings argument '{includeServerSettingsString}'");
            }

            uint updateIntervalSeconds;
            if (!uint.TryParse(updateIntervalSecondsString, out updateIntervalSeconds))
            {
                throw new ArgumentException($"Failed to parse uint from update-interval-seconds argument '{updateIntervalSecondsString}'");
            }
            var updateInterval = TimeSpan.FromSeconds(updateIntervalSeconds);

            ushort exportBindPort;
            if (!ushort.TryParse(exportBindPortString, out exportBindPort))
            {
                throw new ArgumentException($"Failed to parse ushort from export-bind-port argument '{exportBindPortString}'");
            }

            // By default, program metrics will include a ton of .NET diagnostics from _this_ application.
            Metrics.SuppressDefaultMetrics();
            // Configure the metrics factory to expire instrumentation if it hasn't been updated recently.
            // Here we're going to squint and say that "recent" is within two update intervals.
            var metricFactory = Metrics.WithManagedLifetime(expiresAfter: updateInterval * 2);

            logger.LogInformation($"Configuring with exporter at: http://{exportBindHost}:{exportBindPort}{exportPath}");

            // Quirk notes:
            // - .NET's HttpListener rejects the well-known "0.0.0.0" bind host address;
            //   it instead expects the hostname "*" when binding to all local hostnames/addrs.
            // - When Prometheus.MetricServer formats a "prefix" for its underlying HttpListener,
            //   it adds a leading '/'; so remove a leading '/' from our own argument.
            // - The underlying HttpListener requires a trailing '/' on its "prefix" parameter,
            //   even though that trailing '/' isn't a literal requirement for client requests.
            var normalizedBindHost = exportBindHost;
            if (normalizedBindHost.Equals("0.0.0.0", StringComparison.Ordinal))
            {
                normalizedBindHost = "*";
            }
            var normalizedExportPath = exportPath.TrimStart('/');
            if (!normalizedExportPath.EndsWith('/'))
            {
                normalizedExportPath += '/';
            }
            using var exporterServer = new Prometheus.MetricServer(hostname: normalizedBindHost, port: exportBindPort, url: normalizedExportPath);
            exporterServer.Start();

            logger.LogInformation($"Configuring with target PalServer API: http://{palServerHost}:{palServerPort}");
            using (var apiClient = new PalServerApiClient(logger, palServerHost, palServerPort, palServerAdminPassword))
            {
                var metricValues = new PalServerMetricsValues(metricFactory, includePlayerData, ignoreZeroPingPlayers, includeServerSettings);

                // Loop forever, periodically requesting the server's metrics
                // and updating this exporter's instrumentation accordingly.
                while (true)
                {
                    var updateTimer = Stopwatch.StartNew();
                    try
                    {
                        logger.LogDebug("Updating current metric values");

                        // Initiate all PalServer API requests, and wait for them in parallel.
                        var apiTasks = new List<Task>();

                        var infoTask = apiClient.GetInfoAsync();
                        apiTasks.Add(infoTask);

                        var metricsTask = apiClient.GetMetricsAsync();
                        apiTasks.Add(metricsTask);

                        Task<PalServerPlayers> playersTask = null;
                        if (includePlayerData)
                        {
                            playersTask = apiClient.GetPlayersAsync();
                            apiTasks.Add(playersTask);
                        }

                        Task<PalServerSettings> settingsTask = null;
                        if (includeServerSettings)
                        {
                            settingsTask = apiClient.GetSettingsAsync();
                            apiTasks.Add(settingsTask);
                        }

                        await Task.WhenAll(apiTasks);

                        metricValues.Update(infoTask.Result, metricsTask.Result, playersTask?.Result, settingsTask?.Result);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "PalServer API request failed");
                    }

                    updateTimer.Stop();
                    var updateDuration = TimeSpan.FromMilliseconds(updateTimer.ElapsedMilliseconds);
                    if (updateDuration >= updateInterval)
                    {
                        logger.LogWarning($"Metrics update took {updateDuration.Milliseconds} ms, exceeding the delay interval {updateInterval.Milliseconds} ms; next update will start immediately");
                    }
                    else
                    {
                        var timeToNextUpdate = updateInterval - updateDuration;
                        await Task.Delay(timeToNextUpdate);
                    }
                }
            }
        }
    }
}
