using ExampleMod.Common.Configs.CustomDataTypes;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases
{
    [BackgroundColor(148, 72, 188)]
    public class ModConfigShowcaseSubpages : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("SeparatePageExamples")]

        [SeparatePage]
        public Gradient gradient = new Gradient();

        public SubConfigExample subConfigExample = new SubConfigExample();

        [SeparatePage]
        public Dictionary<ItemDefinition, SubConfigExample> DictionaryofSubConfigExample = new Dictionary<ItemDefinition, SubConfigExample>();

        [SeparatePage]
        public List<Pair> SeparateListOfPairs = new List<Pair>();

        [SeparatePage]
        public Pair pair = new Pair();

        [SeparatePage]
        public class SubConfigExample
        {
            [DefaultValue(99)]
            public int boost = 99;
            public float percent;
            public bool enabled;

            [SeparatePage]
            [BackgroundColor(50, 200, 100)]
            public SubSubConfigExample SubA = new SubSubConfigExample();

            [SeparatePage]
            public SubSubConfigExample SubB = new SubSubConfigExample();

            public override string ToString() {
                return $"{boost} {percent} {enabled} {SubA.whoa}/{SubB.whoa}";
            }

            public override bool Equals(object obj) {
                if (obj is SubConfigExample other)
                    return boost == other.boost && percent == other.percent && enabled == other.enabled && SubA.Equals(other.SubA) && SubB.Equals(other.SubB);
                return base.Equals(obj);
            }

            public override int GetHashCode() {
                return new { boost, percent, enabled, SubA, SubB }.GetHashCode();
            }
        }

        public class SubSubConfigExample
        {
            public int whoa;
            public override bool Equals(object obj) {
                if (obj is SubSubConfigExample other)
                    return whoa == other.whoa;
                return base.Equals(obj);
            }

            public override int GetHashCode() => whoa.GetHashCode();
        }
    }
}
