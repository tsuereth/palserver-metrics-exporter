using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mono.Options;

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

            var handleSigterm = false;

            var palServerHost = "localhost";
            var palServerPortString = "8212";
            var palServerAdminPassword = string.Empty;
            var palServerAdminPasswordFile = string.Empty;

            var includePlayerDataString = true.ToString();
            var ignoreZeroPingPlayersString = true.ToString();
            var includeServerSettingsString = true.ToString();
            var includeGameDataString = false.ToString();

            var updateIntervalSecondsString = "15";

            var sourceMapFile = "map-default.png";
            var sourceMapFileContentType = "image/png";

            var bindHost = "0.0.0.0";
            var bindPortString = "8213";
            var serveMetricsPath = "/metrics";
            var serveMapPath = "/map";

            var options = new OptionSet()
            {
                { "help", "Print help text", _ => printHelp = true },
                { "version", "Print application version", _ => printVersion = true },

                { "handle-sigterm", "Handle SIGTERM by gracefully stopping", _ => handleSigterm = true },

                { "palserver-host=", $"Target PalServer host, default: {palServerHost}", o => palServerHost = o },
                { "palserver-port=", $"Target PalServer port, default: {palServerPortString}", o => palServerPortString = o },
                { "palserver-admin-password=", $"Target PalServer AdminPassword, default: {palServerAdminPassword}", o => palServerAdminPassword = o },
                { "palserver-admin-password-file=", $"Path to a file containing the target PalServer AdminPassword, default: {palServerAdminPasswordFile}", o => palServerAdminPasswordFile = o },

                { "include-player-data=", $"Include player data in exported metrics, default: {includePlayerDataString}", o => includePlayerDataString = o },
                { "ignore-zero-ping-players=", $"When reporting player data, ignore data with a ping of zero, default: {ignoreZeroPingPlayersString}", o => ignoreZeroPingPlayersString = o },
                { "include-server-settings=", $"Include server settings in exported metrics, default: {includeServerSettingsString}", o => includeServerSettingsString = o },
                { "include-game-data=", $"Include game data in exported metrics, default: {includeGameDataString}", o => includeGameDataString = o },

                { "update-interval-seconds=", $"Interval (in seconds) between requesting updates from the target PalServer, default: {updateIntervalSecondsString}", o => updateIntervalSecondsString = o },

                { "source-map-file=", $"Path to a source map-image file, default: {sourceMapFile}", o => sourceMapFile = o },
                { "source-map-file-content-type=", $"MIME Content Type of the source map-image file, default: {sourceMapFileContentType}", o => sourceMapFileContentType = o },

                { "bind-host=", $"Hostname or IP address on which to serve metrics, default: {bindHost}", o => bindHost = o },
                { "bind-port=", $"TCP port on which to serve metrics, default: {bindPortString}", o => bindPortString = o },
                { "serve-metrics-path=", $"HTTP path at which to serve metrics, default: {serveMetricsPath}", o => serveMetricsPath = o },
                { "serve-map-path=", $"HTTP path at which to serve map images, default: {serveMapPath}", o => serveMapPath = o },
            };
            var unexpectedArgs = options.Parse(args);
            if (unexpectedArgs.Count > 0)
            {
                var unexpectedArgsString = string.Join(' ', unexpectedArgs);
                throw new ArgumentException($"Unexpected arguments: {unexpectedArgsString}");
            }

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
                throw new ArgumentException($"Failed to parse bool from include-player-data argument '{includePlayerDataString}'");
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
            bool includeGameData;
            if (!bool.TryParse(includeGameDataString, out includeGameData))
            {
                throw new ArgumentException($"Failed to parse bool from include-game-data argument '{includeGameDataString}'");
            }

            uint updateIntervalSeconds;
            if (!uint.TryParse(updateIntervalSecondsString, out updateIntervalSeconds))
            {
                throw new ArgumentException($"Failed to parse uint from update-interval-seconds argument '{updateIntervalSecondsString}'");
            }
            var updateInterval = TimeSpan.FromSeconds(updateIntervalSeconds);

            ushort bindPort;
            if (!ushort.TryParse(bindPortString, out bindPort))
            {
                throw new ArgumentException($"Failed to parse ushort from bind-port argument '{bindPortString}'");
            }

            var tasks = new List<Task>();
            using var cancelSource = new CancellationTokenSource();

            if (handleSigterm)
            {
                using var _ = PosixSignalRegistration.Create(PosixSignal.SIGTERM, (context) =>
                {
                    logger.LogInformation("Received cancel event");
                    cancelSource.Cancel();

                    // Cancel the cancel: let the runtime know we're handling this.
                    context.Cancel = true;
                });
            }

            // Kick off the metrics exporter server.
            using var metricsExporter = new PalServerPrometheusExporter(
                logger,
                palServerHost, palServerPort, palServerAdminPassword,
                includePlayerData, ignoreZeroPingPlayers, includeServerSettings, includeGameData,
                updateInterval,
                bindHost, bindPort, serveMetricsPath);
            tasks.Add(metricsExporter.ServeAsync(cancelSource.Token));

            // And the map server.
            using var mapServer = new PalworldMapServer(
                logger,
                sourceMapFile, sourceMapFileContentType,
                bindHost, bindPort, serveMapPath);
            tasks.Add(mapServer.ServeAsync(cancelSource.Token));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Canceled, shutting down");
            }

            return 0;
        }
    }
}
