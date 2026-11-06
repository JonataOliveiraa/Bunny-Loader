using ExampleMod.Common.Configs.CustomDataTypes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases
{
    [BackgroundColor(164, 153, 190)]
    public class ModConfigShowcaseDefaultValues : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [DefaultValue(99)]
        public int SimpleDefaultInt;

        [DefaultValue(typeof(Color), "73, 94, 171, 255")]
        public Color SomeColor;

        [DefaultValue(typeof(Vector2), "0.23, 0.77")]
        public Vector2 SomeVector2;

        [DefaultValue(SampleEnum.Strange)]
        [DrawTicks]
        public SampleEnum EnumExample2;

        [JsonConverter(typeof(StringEnumConverter))]
        public SampleEnum EnumExample1 { get; set; }

        [DrawTicks]
        [OptionStrings(["Pikachu", "Charmander", "Bulbasaur", "Squirtle"])]
        [DefaultValue("Bulbasaur")]

        public string FavoritePokemon;

        [OptionStrings(["Win", "Lose", "Give Up"])]
        public string[] ArrayOfString = ["Give Up", "Give Up"];

        [DefaultListValue(123)]
        public List<int> ListOfInts = new List<int>();

        [DefaultListValue(typeof(Vector2), "0.1, 0.2")]
        public List<Vector2> ListOfVector2 = new List<Vector2>();

        [JsonDefaultListValue("{\"name\": \"GoldBar\"}")]
        public List<ItemDefinition> ListOfItemDefinition = new List<ItemDefinition>();

        [DefaultDictionaryKeyValue(0.3f)]
        [DefaultListValue(10)]
        public Dictionary<float, int> DictionaryDefaults = new Dictionary<float, int>();

        [JsonDefaultDictionaryKeyValue("{\"name\": \"GoldBar\"}")]
        [JsonDefaultListValue("{\"name\": \"SilverBar\"}")]
        public Dictionary<ItemDefinition, ItemDefinition> DictionaryDefaults2 = new Dictionary<ItemDefinition, ItemDefinition>();
    }
}
