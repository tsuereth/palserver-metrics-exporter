using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/game-data
    // NOTE: Actual results may not match the documentation. :)
    public abstract class PalServerActorData
    {
        public string Type { get; set; }

        [JsonProperty("GuildID")]
        public string GuildId { get; set; }

        public string GuildName { get; set; }

        public string Class { get; set; }

        public double LocationX { get; set; }

        public double LocationY { get; set; }

        public double LocationZ { get; set; }
    }
}
