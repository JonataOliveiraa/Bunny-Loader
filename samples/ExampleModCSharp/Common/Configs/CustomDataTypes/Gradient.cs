using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;

namespace ExampleMod.Common.Configs.CustomDataTypes
{
    public class Gradient
    {
        [DefaultValue(typeof(Color), "0, 0, 255, 255")]
        public Color start = Color.Blue;
        [DefaultValue(typeof(Color), "255, 0, 0, 255")]
        public Color end = Color.Red;

        public override bool Equals(object obj) {
            if (obj is Gradient other)
                return start == other.start && end == other.end;
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return new { start, end }.GetHashCode();
        }
    }
}
