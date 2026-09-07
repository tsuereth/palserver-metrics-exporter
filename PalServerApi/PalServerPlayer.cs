using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // (a subset of) https://docs.palworldgame.com/api/rest-api/players
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerPlayer
    {
        [JsonProperty("name")]
		public string Name { get; set; }

        [JsonProperty("accountName")]
		public string AccountName { get; set; }

        [JsonProperty("playerId")]
		public string PlayerId { get; set; }

        [JsonProperty("userId")]
		public string UserId { get; set; }

        [JsonProperty("iP")]
		public string Ip { get; set; }

        [JsonProperty("ping")]
		public double Ping { get; set; }

        [JsonProperty("location_x")]
		public double LocationX { get; set; }

        [JsonProperty("location_y")]
		public double LocationY { get; set; }

        [JsonProperty("level")]
		public int Level { get; set; }
    }
}
