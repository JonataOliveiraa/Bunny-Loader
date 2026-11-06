using ExampleMod.Common.Configs.CustomDataTypes;
using Newtonsoft.Json;
using Terraria;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases
{
    [BackgroundColor(164, 153, 190)]
    public class ModConfigShowcaseAccessibility : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

#pragma warning disable CS0414
        private float Private = 144;
#pragma warning restore CS0414
        internal float Internal;

        public float Public;

        public static float Static;

        public float Getter => Main.rand.NextFloat(1f);

        public float AutoProperty { get; set; }

        private float propertyBackingField;
        public float Property {
            get { return propertyBackingField; }
            set { propertyBackingField = value + 0.2f; }
        }

        [JsonIgnore]
        public float Ignore;

        [JsonIgnore]
        [ShowDespiteJsonIgnore]
        public float IgnoreWithLabelGetter => AutoProperty + Public;

        [JsonIgnore]
        public Pair pair2 => pair;
        public Pair pair;

        public bool PresetA {
            get => Data1 == 23 && Data2 == 63;
            set {
                if (value) {
                    Data1 = 23;
                    Data2 = 63;
                }
            }
        }

        public bool PresetB {
            get => Data1 == 93 && Data2 == 13;
            set {
                if (value) {
                    Data1 = 93;
                    Data2 = 13;
                }
            }
        }

        [Slider]
        public int Data1 { get; set; }
        [Slider]
        public int Data2 { get; set; }

        public ModConfigShowcaseAccessibility() {
            Internal = 0.2f;
        }

        public bool ShouldSerializeGetter() {
            return false;
        }
    }
}
