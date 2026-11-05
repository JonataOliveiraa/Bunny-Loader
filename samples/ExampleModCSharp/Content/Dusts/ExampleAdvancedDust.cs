using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Dusts
{
    
    internal class ExampleAdvancedDust : ModDust
    {
        
        public override string Texture => null; 

        public override void OnSpawn(Dust dust) {
            dust.noGravity = true;

            int desiredVanillaDustTexture = DustID.Confetti;
            int frameX = desiredVanillaDustTexture * 10 % 1000;
            int frameY = desiredVanillaDustTexture * 10 / 1000 * 30 + Main.rand.Next(3) * 10;
            dust.frame = new Rectangle(frameX, frameY, 8, 8);

            dust.velocity = Vector2.Zero;
        }

        public override bool Update(Dust dust) {
            
            dust.rotation += 0.1f * (dust.dustIndex % 2 == 0 ? -1 : 1);
            dust.scale -= 0.05f;

            if (!dust.customData.IsNull && Player.Is(dust.customData)) {
                
                dust.position = new Player(dust.customData.Pointer).Center + Vector2.UnitX.RotatedBy(dust.rotation, Vector2.Zero) * dust.scale * 50;
            }

            if (dust.scale < 0.25f)
                dust.active = false;

            return false;
        }
    }
}
