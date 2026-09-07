using System.Collections.Generic;
using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/players
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerPlayers
    {
        [JsonProperty("players")]
        public List<PalServerPlayer> Players { get; set; } = new List<PalServerPlayer>();
    }
}
