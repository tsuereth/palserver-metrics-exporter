using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PalServerMetricsExporter.PalServerApi
{
    public class PalServerActorDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(PalServerActorData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var untypedObject = JObject.Load(reader);
            string typeString;
            if (untypedObject.TryGetValue("Type", out var typeToken))
            {
                typeString = typeToken.Value<string>();
            }
            else
            {
                throw new InvalidDataContractException("Failed to extract Type string");
            }

            PalServerActorData actorData;
            switch (typeString)
            {
                case PalServerCharacterActor.CharacterActorType:
                    actorData = new PalServerCharacterActor();
                    break;
                case PalServerPalBoxActor.PalBoxActorType:
                    actorData = new PalServerPalBoxActor();
                    break;
                default:
                    throw new InvalidDataContractException($"Unrecognized Type string '{typeString}'");
            }

            serializer.Populate(untypedObject.CreateReader(), actorData);
            return actorData;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
