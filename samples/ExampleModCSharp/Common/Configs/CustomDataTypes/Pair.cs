using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.CustomDataTypes
{
    [BackgroundColor(0, 255, 255)]
    public class Pair
    {
        public bool enabled;
        public int boost;

        public override string ToString() {
            return $"Boost: {(enabled ? "" + boost : "disabled")}";
        }

        public override bool Equals(object obj) {
            if (obj is Pair other)
                return enabled == other.enabled && boost == other.boost;
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return new { boost, enabled }.GetHashCode();
        }
    }
}
