using System.Collections.Generic;
using System.Linq;
using PalServerMetricsExporter.PalServerApi;
using Prometheus;

namespace PalServerMetricsExporter
{
    public class PalServerMetricsValues
    {
        private const string MetricNamePrefix = "palserver_";

        private readonly IManagedLifetimeMetricFactory metricFactory;

        private bool includePlayerData;
        private bool ignoreZeroPingPlayers;
        private bool includeServerSettings;

        private OrderedDictionary<string, string> labels = new();

        // Server operational and performance metrics
        private ICollector<IGauge> currentPlayerNum;
        private ICollector<IGauge> serverFps;
        private ICollector<IGauge> serverFrameTime;
        private ICollector<IGauge> days;
        private ICollector<IGauge> maxPlayerNum;
        private ICollector<IGauge> baseCampNum;
        private ICollector<IGauge> uptime;

        // Per-player metrics
        private Dictionary<string, OrderedDictionary<string, string>> playerLabelsById = new();
        private Dictionary<string, ICollector<IGauge>> playerPingById = new();
        private Dictionary<string, ICollector<IGauge>> playerLocationXById = new();
        private Dictionary<string, ICollector<IGauge>> playerLocationYById = new();
        private Dictionary<string, ICollector<IGauge>> playerLevelById = new();

        // Server settings metrics
        private ICollector<IGauge> baseCampMaxNum;
        private ICollector<IGauge> baseCampMaxNumInGuild;
        private ICollector<IGauge> baseCampWorkerMaxNum;
        private ICollector<IGauge> itemContainerForceMarkDirtyInterval;
        private ICollector<IGauge> maxBuildingLimitNum;
        private ICollector<IGauge> physicsActiveDropItemMaxNum;
        private ICollector<IGauge> serverReplicatePawnCullDistance;

        public PalServerMetricsValues(IManagedLifetimeMetricFactory metricFactory, bool includePlayerData, bool ignoreZeroPingPlayers, bool includeServerSettings)
        {
            this.metricFactory = metricFactory;

            this.includePlayerData = includePlayerData;
            this.ignoreZeroPingPlayers = ignoreZeroPingPlayers;
            this.includeServerSettings = includeServerSettings;
        }

        private static OrderedDictionary<string, string> FormatServerLabels(PalServerInfo info)
        {
            var labels = new OrderedDictionary<string, string>();

            labels["server_version"] = info.Version;
            labels["world_guid"] = info.WorldGuid;

            return labels;
        }

        private void CreateServerMetrics()
        {
            var labelNames = this.labels.Keys.ToArray();

            this.currentPlayerNum = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}current_player_num",
                "The current number of players connected",
                labelNames).WithExtendLifetimeOnUse();

            this.serverFps = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}server_fps",
                "The server's current runtime frames per second",
                labelNames).WithExtendLifetimeOnUse();

            this.serverFrameTime = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}server_frame_time_seconds",
                "The server's processing time between frames",
                labelNames).WithExtendLifetimeOnUse();

            this.days = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}days",
                "The number of in-game days which have passed in the server's game world",
                labelNames).WithExtendLifetimeOnUse();

            this.maxPlayerNum = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}max_player_num",
                "The maximum amount of players allowed on the server",
                labelNames).WithExtendLifetimeOnUse();

            this.baseCampNum = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}base_camp_num",
                "The current number of base camps",
                labelNames).WithExtendLifetimeOnUse();

            this.uptime = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}uptime_seconds",
                "The server's uptime",
                labelNames).WithExtendLifetimeOnUse();

            if (this.includeServerSettings)
            {
                this.baseCampMaxNum = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}base_camp_max_num",
                    "The maximum amount of base camps allowed in the server's game world",
                    labelNames).WithExtendLifetimeOnUse();

                this.baseCampMaxNumInGuild = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}base_camp_max_num_in_guild",
                    "The maximum amount of base camps allowed per guild",
                    labelNames).WithExtendLifetimeOnUse();

                this.baseCampWorkerMaxNum = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}base_camp_worker_max_num",
                    "The maximum amount of workers allowed in a base camp",
                    labelNames).WithExtendLifetimeOnUse();

                this.itemContainerForceMarkDirtyInterval = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}item_container_force_mark_dirty_interval_seconds",
                    "Synchronization interval when a player is viewing a container's contents",
                    labelNames).WithExtendLifetimeOnUse();

                this.maxBuildingLimitNum = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}max_building_limit_num",
                    "The maximum amount of buildings allowed per player",
                    labelNames).WithExtendLifetimeOnUse();

                this.physicsActiveDropItemMaxNum = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}physics_active_drop_item_max_num",
                    "The maximum amount of dropped items which can have active physics behavior",
                    labelNames).WithExtendLifetimeOnUse();

                this.serverReplicatePawnCullDistance = this.metricFactory.CreateGauge(
                    $"{MetricNamePrefix}server_replicate_pawn_cull_distance",
                    "The world distance within which players can see enemies, pals, and other players",
                    labelNames).WithExtendLifetimeOnUse();
            }
        }

        private static OrderedDictionary<string, string> FormatPlayerLabels(OrderedDictionary<string, string> serverLabels, PalServerPlayer player)
        {
            var labels = new OrderedDictionary<string, string>(serverLabels);

            labels.Add("player_account_name", player.AccountName);
            labels.Add("player_ip", player.Ip);
            labels.Add("player_name", player.Name);
            labels.Add("player_player_id", player.PlayerId);
            labels.Add("player_user_id", player.UserId);

            return labels;
        }

        private void CreatePlayerMetrics(string playerId)
        {
            var labelNames = this.playerLabelsById[playerId].Keys.ToArray();

            this.playerPingById[playerId] = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}player_ping_seconds",
                "Player's ping to the game server",
                labelNames).WithExtendLifetimeOnUse();

            this.playerLocationXById[playerId] = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}player_location_x",
                "Player's x-axis world coordinate",
                labelNames).WithExtendLifetimeOnUse();

            this.playerLocationYById[playerId] = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}player_location_y",
                "Player's y-axis world coordinate",
                labelNames).WithExtendLifetimeOnUse();

            this.playerLevelById[playerId] = this.metricFactory.CreateGauge(
                $"{MetricNamePrefix}player_level",
                "Player's character level",
                labelNames).WithExtendLifetimeOnUse();
        }

        public void Update(PalServerInfo info, PalServerMetrics metrics, PalServerPlayers players, PalServerSettings settings)
        {
            var newLabels = FormatServerLabels(info);

            // If labels have changed, then register new metrics with those labels.
            if (newLabels.Count != this.labels.Count || newLabels.Except(this.labels).Any())
            {
                this.labels = newLabels;
                this.CreateServerMetrics();
            }

            var labelValues = this.labels.Values.ToArray();

            this.currentPlayerNum.WithLabels(labelValues).Set(metrics.CurrentPlayerNum);
            this.serverFps.WithLabels(labelValues).Set(metrics.ServerFps);
            this.serverFrameTime.WithLabels(labelValues).Set(metrics.ServerFrameTime / 1000.0);
            this.days.WithLabels(labelValues).Set(metrics.Days);
            this.maxPlayerNum.WithLabels(labelValues).Set(metrics.MaxPlayerNum);
            this.baseCampNum.WithLabels(labelValues).Set(metrics.BaseCampNum);
            this.uptime.WithLabels(labelValues).Set(metrics.Uptime);

            if (this.includePlayerData)
            {
                // Check if each previously-seen player ID has disconnected.
                var disconnectedPlayerIds = new HashSet<string>(this.playerLabelsById.Keys);

                foreach (var player in players.Players)
                {
                    disconnectedPlayerIds.Remove(player.PlayerId);

                    if (this.ignoreZeroPingPlayers)
                    {
                        // When a player is just logging into the game, and their client is still loading,
                        // their ping will be reported as 0; this isn't a real measurement, and can be ignored.
                        if (player.Ping == 0.0)
                        {
                            continue;
                        }
                    }

                    // If the player hasn't been seen before OR if labels have changed, then register new metrics for the player.
                    var newPlayerLabels = FormatPlayerLabels(this.labels, player);
                    var shouldCreatePlayerMetrics = false;
                    if (!this.playerLabelsById.TryGetValue(player.PlayerId, out var playerLabels))
                    {
                        shouldCreatePlayerMetrics = true;
                    }
                    else if (newPlayerLabels.Count != playerLabels.Count || newPlayerLabels.Except(playerLabels).Any())
                    {
                        shouldCreatePlayerMetrics = true;
                    }
                    if (shouldCreatePlayerMetrics)
                    {
                        this.playerLabelsById[player.PlayerId] = newPlayerLabels;
                        this.CreatePlayerMetrics(player.PlayerId);
                    }

                    var playerLabelValues = this.playerLabelsById[player.PlayerId].Values.ToArray();

                    this.playerPingById[player.PlayerId].WithLabels(playerLabelValues).Set(player.Ping / 1000.0);
                    this.playerLocationXById[player.PlayerId].WithLabels(playerLabelValues).Set(player.LocationX);
                    this.playerLocationYById[player.PlayerId].WithLabels(playerLabelValues).Set(player.LocationY);
                    this.playerLevelById[player.PlayerId].WithLabels(playerLabelValues).Set(player.Level);
                }

                // Forget metrics for previously-seen players who are no longer connected.
                foreach (var playerId in disconnectedPlayerIds)
                {
                    this.playerLabelsById.Remove(playerId);
                    this.playerPingById.Remove(playerId);
                    this.playerLocationXById.Remove(playerId);
                    this.playerLocationYById.Remove(playerId);
                    this.playerLevelById.Remove(playerId);
                }
            }

            if (this.includeServerSettings)
            {
                this.baseCampMaxNum.WithLabels(labelValues).Set(settings.BaseCampMaxNum);
                this.baseCampMaxNumInGuild.WithLabels(labelValues).Set(settings.BaseCampMaxNumInGuild);
                this.baseCampWorkerMaxNum.WithLabels(labelValues).Set(settings.BaseCampWorkerMaxNum);
                this.itemContainerForceMarkDirtyInterval.WithLabels(labelValues).Set(settings.ItemContainerForceMarkDirtyInterval);
                this.maxBuildingLimitNum.WithLabels(labelValues).Set(settings.MaxBuildingLimitNum);
                this.physicsActiveDropItemMaxNum.WithLabels(labelValues).Set(settings.PhysicsActiveDropItemMaxNum);
                this.serverReplicatePawnCullDistance.WithLabels(labelValues).Set(settings.ServerReplicatePawnCullDistance);
            }

            // TODO: When the server is running with `-enable-gamedata-api`, detailed
            // game-state data is available from its `/v1/api/game-data` response.
        }
    }
}
