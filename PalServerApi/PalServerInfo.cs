using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/info
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerInfo
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("worldguid")]
        public string WorldGuid { get; set; }
    }
}
