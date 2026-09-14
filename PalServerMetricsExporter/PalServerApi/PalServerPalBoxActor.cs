using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/game-data
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerPalBoxActor : PalServerActorData
    {
        public const string PalBoxActorType = "PalBox";

        public string Name { get; set; }
    }
}
