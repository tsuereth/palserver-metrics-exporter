using System.Collections.Generic;
using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/game-data
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerGameData
    {
        public string Time { get; set; }

        [JsonProperty("FPS")]
        public double Fps { get; set; }

        [JsonProperty("AverageFPS")]
        public double AverageFps { get; set; }

        public string InGameTime { get; set; }

        public int InGameDays { get; set; }

        public List<PalServerActorData> ActorData { get; set; } = new();
    }
}
