using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/metrics
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerMetrics
    {
        [JsonProperty("currentplayernum")]
        public int CurrentPlayerNum { get; set; }

        [JsonProperty("serverfps")]
        public int ServerFps { get; set; }

        [JsonProperty("serverfpsaverage")]
        public double ServerFpsAverage { get; set; }

        [JsonProperty("serverframetime")]
        public double ServerFrameTime { get; set; }

        [JsonProperty("days")]
        public int Days { get; set; }

        [JsonProperty("maxplayernum")]
        public int MaxPlayerNum { get; set; }

        [JsonProperty("basecampnum")]
        public int BaseCampNum { get; set; }

        [JsonProperty("uptime")]
        public int Uptime { get; set; }
    }
}
