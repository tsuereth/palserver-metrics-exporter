using Newtonsoft.Json;

namespace PalServerMetricsExporter.PalServerApi
{
    // https://docs.palworldgame.com/api/rest-api/game-data
    // NOTE: Actual results may not match the documentation. :)
    public class PalServerCharacterActor : PalServerActorData
    {
        public const string CharacterActorType = "Character";

        public static readonly string[] UnitTypes =
        {
            "BaseCampPal",
            "NPC",
            "OtomoPal",
            "Player",
            "WildPal",
        };

        [JsonProperty("InstanceID")]
        public string InstanceId { get; set; }

        public string UnitType { get; set; }

        public string NickName { get; set; }

        [JsonProperty("TrainerInstanceID")]
        public string TrainerInstanceId { get; set; }

        public string TrainerNickName { get; set; }

        public string TrainerClass { get; set; }

        [JsonProperty("userid")]
        public string UserId { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("HP")]
        public int Hp { get; set; }

        [JsonProperty("MaxHP")]
        public int MaxHp { get; set; }

        public string Action { get; set; }

        [JsonProperty("AI_Action")]
        public string AiAction { get; set; }

        public double RotationX { get; set; }

        public double RotationY { get; set; }

        public double RotationZ { get; set; }

        public string Stage { get; set; }

        public string IsActive { get; set; }
    }
}
