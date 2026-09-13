using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using PalServerMetricsExporter.PalServerApi;

namespace PalServerMetricsExporter.Test.PalServerApi
{
    [TestClass]
    public class PalServerActorDataJsonConverterTests
    {
        static readonly string TestFilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");

        static JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Converters = new List<JsonConverter>()
                {
                    new PalServerActorDataJsonConverter(),
                },
            };
        }

        [TestMethod]
        public void TestCanConvert()
        {
            var jsonConverter = new PalServerActorDataJsonConverter();

            Assert.IsFalse(jsonConverter.CanConvert(typeof(string)));
            Assert.IsFalse(jsonConverter.CanConvert(typeof(PalServerInfo)));

            Assert.IsTrue(jsonConverter.CanConvert(typeof(PalServerActorData)));
            Assert.IsTrue(jsonConverter.CanConvert(typeof(PalServerCharacterActor)));
            Assert.IsTrue(jsonConverter.CanConvert(typeof(PalServerPalBoxActor)));
        }

        [TestMethod]
        public void TestReadJsonValidData()
        {
            var filePath = Path.Combine(TestFilesDirectory, "test-actor-data.json");
            var testString = File.ReadAllText(filePath);

            var jsonSettings = CreateJsonSettings();
            var result = JsonConvert.DeserializeObject<List<PalServerActorData>>(testString, jsonSettings);

            Assert.IsNotEmpty(result);

            foreach (var resultItem in result)
            {
                Assert.IsInstanceOfType<PalServerActorData>(resultItem);

                switch (resultItem.Type)
                {
                    case PalServerCharacterActor.CharacterActorType:
                        Assert.IsInstanceOfType<PalServerCharacterActor>(resultItem);
                        break;
                    case PalServerPalBoxActor.PalBoxActorType:
                        Assert.IsInstanceOfType<PalServerPalBoxActor>(resultItem);
                        break;
                    default:
                        Assert.Fail($"Unrecognized Type '{resultItem.Type}'");
                        break;
                }
            }
        }

        [TestMethod]
        public void TestReadJsonMissingType()
        {
            var testString = "{\"just\":\"some\",\"arbitrary\":\"json\"}";

            var jsonSettings = CreateJsonSettings();
            Assert.ThrowsExactly<InvalidDataContractException>(() =>
            {
                _ = JsonConvert.DeserializeObject<PalServerActorData>(testString, jsonSettings);
            });
        }

        [TestMethod]
        public void TestReadJsonUnrecognizedType()
        {
            var testString = "{\"Type\":\"NotARealType\",\"Class\":\"BP_Bogus_C\"}";

            var jsonSettings = CreateJsonSettings();
            Assert.ThrowsExactly<InvalidDataContractException>(() =>
            {
                _ = JsonConvert.DeserializeObject<PalServerActorData>(testString, jsonSettings);
            });
        }
    }
}
