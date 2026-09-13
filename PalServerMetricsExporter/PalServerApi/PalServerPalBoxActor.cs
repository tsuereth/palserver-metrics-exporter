using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/game-data
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerPalBoxActor : PalServerActorData
    {
        public const string PalBoxActorType = "PalBox";

        // NOTE: This actor data-type has no additional properties.
    }
}
