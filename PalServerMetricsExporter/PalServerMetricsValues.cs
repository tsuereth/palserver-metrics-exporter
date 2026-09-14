using System.Collections.Generic;
using System.Linq;
using PalServerMetricsExporter.PalServerApi;
using Prometheus;

namespace PalServerMetricsExporter
{
    public class PalServerMetricsValues
    {
        private const string MetricNamePrefix = "palserver_";

        private const string UnknownActorType = "unknown";

        private readonly IManagedLifetimeMetricFactory metricFactory;

        private bool includePlayerData;
        private bool ignoreZeroPingPlayers;
        private bool includeServerSettings;
        private bool includeGameData;

        // Server operational and performance metrics
        private ICollector<IGauge> infoMetric;
        private OrderedDictionary<string, string> infoLabels = new();
        private ICollector<IGauge> currentPlayerNum;
        private ICollector<IGauge> serverFps;
        private ICollector<IGauge> serverFrameTime;
        private ICollector<IGauge> days;
        private ICollector<IGauge> maxPlayerNum;
        private ICollector<IGauge> baseCampNum;
        private ICollector<IGauge> uptime;

        // Per-player metrics
        private sealed class PlayerMetrics
        {
            public ICollector<IGauge> infoMetric;
            public OrderedDictionary<string, string> infoLabels = new();
            public ICollector<IGauge> ping;
            public ICollector<IGauge> locationX;
            public ICollector<IGauge> locationY;
            public ICollector<IGauge> level;
        }

        private Dictionary<string, PlayerMetrics> playerMetricsByUserId = new();

        // Server settings metrics
        private ICollector<IGauge> baseCampMaxNum;
        private ICollector<IGauge> baseCampMaxNumInGuild;
        private ICollector<IGauge> baseCampWorkerMaxNum;
        private ICollector<IGauge> itemContainerForceMarkDirtyInterval;
        private ICollector<IGauge> maxBuildingLimitNum;
        private ICollector<IGauge> physicsActiveDropItemMaxNum;
        private ICollector<IGauge> serverReplicatePawnCullDistance;

        // Game data metrics
        private HashSet<string> actorTypes = new();
        private ICollector<IGauge> actorNum;

        public PalServerMetricsValues(
            IManagedLifetimeMetricFactory metricFactory,
            bool includePlayerData,
            bool ignoreZeroPingPlayers,
            bool includeServerSettings,
            bool includeGameData)
        {
            this.metricFactory = metricFactory;

            this.includePlayerData = includePlayerData;
            this.ignoreZeroPingPlayers = ignoreZeroPingPlayers;
            this.includeServerSettings = includeServerSettings;
            this.includeGameData = includeGameData;
        }

        private void PrepareServerMetrics(PalServerInfo info)
        {
            var newInfoLabels = new OrderedDictionary<string, string>();
            newInfoLabels["server_version"] = info.Version;
            newInfoLabels["world_guid"] = info.WorldGuid;

            // If info labels have changed, then re-create the info metric.
            var forceCreateInfoMetric = false;
            if (newInfoLabels.Count != this.infoLabels.Count || newInfoLabels.Except(this.infoLabels).Any())
            {
                this.infoLabels = newInfoLabels;
                forceCreateInfoMetric = true;
            }

            if (forceCreateInfoMetric || this.infoMetric == null)
            {
                this.infoMetric = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}info",
                        "Server info",
                        this.infoLabels.Keys.ToArray())
                    .WithExtendLifetimeOnUse();
            }

            if (this.currentPlayerNum == null)
            {
                this.currentPlayerNum = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}current_player_num",
                        "The current number of players connected")
                    .WithExtendLifetimeOnUse();
            }

            if (this.serverFps == null)
            {
                this.serverFps = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}server_fps",
                        "The server's current runtime frames per second")
                    .WithExtendLifetimeOnUse();
            }

            if (this.serverFrameTime == null)
            {
                this.serverFrameTime = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}server_frame_time_seconds",
                        "The server's processing time between frames")
                    .WithExtendLifetimeOnUse();
            }

            if (this.days == null)
            {
                this.days = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}days",
                        "The number of in-game days which have passed in the server's game world")
                    .WithExtendLifetimeOnUse();
            }

            if (this.maxPlayerNum == null)
            {
                this.maxPlayerNum = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}max_player_num",
                        "The maximum amount of players allowed on the server")
                    .WithExtendLifetimeOnUse();
            }

            if (this.baseCampNum == null)
            {
                this.baseCampNum = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}base_camp_num",
                        "The current number of base camps")
                    .WithExtendLifetimeOnUse();
            }

            if (this.uptime == null)
            {
                this.uptime = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}uptime_seconds",
                        "The server's uptime")
                    .WithExtendLifetimeOnUse();
            }

            if (this.includeServerSettings)
            {
                if (this.baseCampMaxNum == null)
                {
                    this.baseCampMaxNum = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}base_camp_max_num",
                            "The maximum amount of base camps allowed in the server's game world")
                        .WithExtendLifetimeOnUse();
                }

                if (this.baseCampMaxNumInGuild == null)
                {
                    this.baseCampMaxNumInGuild = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}base_camp_max_num_in_guild",
                            "The maximum amount of base camps allowed per guild")
                        .WithExtendLifetimeOnUse();
                }

                if (this.baseCampWorkerMaxNum == null)
                {
                    this.baseCampWorkerMaxNum = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}base_camp_worker_max_num",
                            "The maximum amount of workers allowed in a base camp")
                        .WithExtendLifetimeOnUse();
                }

                if (this.itemContainerForceMarkDirtyInterval == null)
                {
                    this.itemContainerForceMarkDirtyInterval = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}item_container_force_mark_dirty_interval_seconds",
                            "Synchronization interval when a player is viewing a container's contents")
                        .WithExtendLifetimeOnUse();
                }

                if (this.maxBuildingLimitNum == null)
                {
                    this.maxBuildingLimitNum = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}max_building_limit_num",
                            "The maximum amount of buildings allowed per player")
                        .WithExtendLifetimeOnUse();
                }

                if (this.physicsActiveDropItemMaxNum == null)
                {
                    this.physicsActiveDropItemMaxNum = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}physics_active_drop_item_max_num",
                            "The maximum amount of dropped items which can have active physics behavior")
                        .WithExtendLifetimeOnUse();
                }

                if (this.serverReplicatePawnCullDistance == null)
                {
                    this.serverReplicatePawnCullDistance = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}server_replicate_pawn_cull_distance",
                            "The world distance within which players can see enemies, pals, and other players")
                        .WithExtendLifetimeOnUse();
                }
            }

            if (this.includeGameData)
            {
                // NOTE: At present, possible actor types are defined at compile-time.
                // They "could" be dynamically discovered, hypothetically speaking.
                var newActorTypes = new HashSet<string>();
                foreach (var actorType in PalServerCharacterActor.UnitTypes)
                {
                    newActorTypes.Add(actorType);
                }
                newActorTypes.Add(PalServerPalBoxActor.PalBoxActorType);

                // If actor types have changed, then re-create actor metrics.
                var forceCreateActorMetrics = false;
                if (newActorTypes.Count != this.actorTypes.Count || newActorTypes.Except(this.actorTypes).Any())
                {
                    this.actorTypes = newActorTypes;
                    forceCreateActorMetrics = true;
                }

                if (forceCreateActorMetrics || this.actorNum == null)
                {
                    this.actorNum = this.metricFactory
                        .CreateGauge(
                            $"{MetricNamePrefix}actor_num",
                            "The current number of actors in game data",
                            ["actor_type"])
                        .WithExtendLifetimeOnUse();
                }
            }
        }

        private void PreparePlayerMetrics(PalServerPlayer player)
        {
            var userId = player.UserId;

            var newPlayerInfoLabels = new OrderedDictionary<string, string>();
            newPlayerInfoLabels["player_account_name"] = player.AccountName;
            newPlayerInfoLabels["player_id"] = player.PlayerId;
            newPlayerInfoLabels["player_ip"] = player.Ip;
            newPlayerInfoLabels["player_name"] = player.Name;
            newPlayerInfoLabels["player_user_id"] = player.UserId;

            // If player info labels have changed, then re-create their info metric.
            var forceCreatePlayerInfoMetric = false;
            if (!this.playerMetricsByUserId.ContainsKey(userId))
            {
                this.playerMetricsByUserId.Add(userId, new PlayerMetrics());
                this.playerMetricsByUserId[userId].infoLabels = newPlayerInfoLabels;
                forceCreatePlayerInfoMetric = true;
            }
            else
            {
                if (newPlayerInfoLabels.Count != this.playerMetricsByUserId[userId].infoLabels.Count || newPlayerInfoLabels.Except(this.playerMetricsByUserId[userId].infoLabels).Any())
                {
                    this.playerMetricsByUserId[userId].infoLabels = newPlayerInfoLabels;
                    forceCreatePlayerInfoMetric = true;
                }
            }

            if (forceCreatePlayerInfoMetric || this.playerMetricsByUserId[userId].infoMetric == null)
            {
                this.playerMetricsByUserId[userId].infoMetric = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}player_info",
                        "Player info",
                        this.playerMetricsByUserId[userId].infoLabels.Keys.ToArray())
                    .WithExtendLifetimeOnUse();
            }

            if (this.playerMetricsByUserId[userId].ping == null)
            {
                this.playerMetricsByUserId[userId].ping = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}player_ping_seconds",
                        "Player's ping to the game server",
                        ["player_user_id"])
                    .WithExtendLifetimeOnUse();
            }

            if (this.playerMetricsByUserId[userId].locationX == null)
            {
                this.playerMetricsByUserId[userId].locationX = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}player_location_x",
                        "Player's x-axis world coordinate",
                        ["player_user_id"])
                    .WithExtendLifetimeOnUse();
            }

            if (this.playerMetricsByUserId[userId].locationY == null)
            {
                this.playerMetricsByUserId[userId].locationY = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}player_location_y",
                        "Player's y-axis world coordinate",
                        ["player_user_id"])
                    .WithExtendLifetimeOnUse();
            }

            if (this.playerMetricsByUserId[userId].level == null)
            {
                this.playerMetricsByUserId[userId].level = this.metricFactory
                    .CreateGauge(
                        $"{MetricNamePrefix}player_level",
                        "Player's character level",
                        ["player_user_id"])
                    .WithExtendLifetimeOnUse();
            }
        }

        public void Update(
            PalServerInfo info,
            PalServerMetrics metrics,
            PalServerPlayers players,
            PalServerSettings settings,
            PalServerGameData gameData)
        {
            this.PrepareServerMetrics(info);

            this.infoMetric.WithLabels(this.infoLabels.Values.ToArray()).Set(1.0);
            this.currentPlayerNum.WithLabels().Set(metrics.CurrentPlayerNum);
            this.serverFps.WithLabels().Set(metrics.ServerFps);
            this.serverFrameTime.WithLabels().Set(metrics.ServerFrameTime / 1000.0);
            this.days.WithLabels().Set(metrics.Days);
            this.maxPlayerNum.WithLabels().Set(metrics.MaxPlayerNum);
            this.baseCampNum.WithLabels().Set(metrics.BaseCampNum);
            this.uptime.WithLabels().Set(metrics.Uptime);

            if (this.includePlayerData)
            {
                // Check if each previously-seen user ID has disconnected.
                var disconnectedUserIds = new HashSet<string>(this.playerMetricsByUserId.Keys);

                foreach (var player in players.Players)
                {
                    // If our key (the user ID) is empty, there's nothing we can do with this player.
                    var userId = player.UserId;
                    if (string.IsNullOrEmpty(userId))
                    {
                        continue;
                    }

                    disconnectedUserIds.Remove(userId);

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
                    this.PreparePlayerMetrics(player);

                    this.playerMetricsByUserId[userId].infoMetric.WithLabels(this.playerMetricsByUserId[userId].infoLabels.Values.ToArray()).Set(1.0);
                    this.playerMetricsByUserId[userId].ping.WithLabels([userId]).Set(player.Ping / 1000.0);
                    this.playerMetricsByUserId[userId].locationX.WithLabels([userId]).Set(player.LocationX);
                    this.playerMetricsByUserId[userId].locationY.WithLabels([userId]).Set(player.LocationY);
                    this.playerMetricsByUserId[userId].level.WithLabels([userId]).Set(player.Level);
                }

                // Forget metrics for previously-seen players who are no longer connected.
                foreach (var userId in disconnectedUserIds)
                {
                    this.playerMetricsByUserId.Remove(userId);
                }
            }

            if (this.includeServerSettings)
            {
                this.baseCampMaxNum.WithLabels().Set(settings.BaseCampMaxNum);
                this.baseCampMaxNumInGuild.WithLabels().Set(settings.BaseCampMaxNumInGuild);
                this.baseCampWorkerMaxNum.WithLabels().Set(settings.BaseCampWorkerMaxNum);
                this.itemContainerForceMarkDirtyInterval.WithLabels().Set(settings.ItemContainerForceMarkDirtyInterval);
                this.maxBuildingLimitNum.WithLabels().Set(settings.MaxBuildingLimitNum);
                this.physicsActiveDropItemMaxNum.WithLabels().Set(settings.PhysicsActiveDropItemMaxNum);
                this.serverReplicatePawnCullDistance.WithLabels().Set(settings.ServerReplicatePawnCullDistance);
            }

            if (this.includeGameData)
            {
                // Ensure an initial value for each known actor-type.
                var actorNumByType = new Dictionary<string, int>();
                foreach (var actorType in this.actorTypes)
                {
                    actorNumByType[actorType] = 0;
                }
                actorNumByType[UnknownActorType] = 0;

                foreach (var gameDataActor in gameData.ActorData)
                {
                    string actorType;
                    if (gameDataActor is PalServerCharacterActor)
                    {
                        var characterActor = gameDataActor as PalServerCharacterActor;
                        actorType = characterActor.UnitType;
                    }
                    else if (gameDataActor is PalServerPalBoxActor)
                    {
                        actorType = PalServerPalBoxActor.PalBoxActorType;
                    }
                    else
                    {
                        actorType = UnknownActorType;
                    }

                    if (this.actorTypes.Contains(actorType))
                    {
                        actorNumByType[actorType] = actorNumByType[actorType] + 1;
                    }
                    else
                    {
                        actorNumByType[UnknownActorType] = actorNumByType[UnknownActorType] + 1;
                    }
                }

                foreach (var actorNumPair in actorNumByType)
                {
                    this.actorNum.WithLabels(actorNumPair.Key).Set(actorNumPair.Value);
                }
            }
        }
    }
}
