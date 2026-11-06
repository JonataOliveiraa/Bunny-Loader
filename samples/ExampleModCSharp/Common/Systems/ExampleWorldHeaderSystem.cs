using ExampleMod.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Systems
{
    public class ExampleWorldHeaderSystem : ModSystem
    {
        public override void SaveWorldHeader(TagCompound tag) {
            tag["ExampleModExists"] = true;
        }
    }
}
