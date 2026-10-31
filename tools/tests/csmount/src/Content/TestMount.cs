using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMount.Content;

public class TestMountBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoTimeDisplay[Type] = true;
        Main.buffNoSave[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.mount.SetMount(ModContent.MountType<TestMount>(), player);
        player.buffTime[buffIndex] = 10;
    }
}

public class TestMount : ModMount
{
    public sealed class Data
    {
        public int Ticks;
    }

    public static readonly Dictionary<string, int> Calls = new();
    public static int TextureWidth = -1, JumpHeightSeen = -1;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override void SetStaticDefaults()
    {
        Count(nameof(SetStaticDefaults));
        MountData.jumpHeight = 5;
        MountData.acceleration = 0.19f;
        MountData.jumpSpeed = 4f;
        MountData.blockExtraJumps = false;
        MountData.constantJump = true;
        MountData.heightBoost = 20;
        MountData.fallDamage = 0.5f;
        MountData.runSpeed = 11f;
        MountData.dashSpeed = 8f;
        MountData.flightTimeMax = 0;
        MountData.fatigueMax = 0;
        MountData.buff = ModContent.BuffType<TestMountBuff>();
        MountData.spawnDust = DustID.Cloud;
        MountData.totalFrames = 4;
        MountData.playerYOffsets = Enumerable.Repeat(20, MountData.totalFrames).ToArray();
        MountData.xOffset = 13;
        MountData.yOffset = -12;
        MountData.playerHeadOffset = 22;
        MountData.bodyFrame = 3;
        MountData.standingFrameCount = 4;
        MountData.standingFrameDelay = 12;
        MountData.standingFrameStart = 0;
        MountData.runningFrameCount = 4;
        MountData.runningFrameDelay = 12;
        MountData.runningFrameStart = 0;
        MountData.inAirFrameCount = 1;
        MountData.inAirFrameDelay = 12;
        MountData.inAirFrameStart = 0;
        MountData.idleFrameCount = 4;
        MountData.idleFrameDelay = 12;
        MountData.idleFrameStart = 0;
        MountData.idleFrameLoop = true;
        MountData.swimFrameCount = MountData.inAirFrameCount;
        MountData.swimFrameDelay = MountData.inAirFrameDelay;
        MountData.swimFrameStart = MountData.inAirFrameStart;
        MountData.textureWidth = MountData.backTexture.Width() + 20;
        MountData.textureHeight = MountData.backTexture.Height();
        TextureWidth = MountData.textureWidth;
    }

    public override void SetMount(Player player, ref bool skipDust)
    {
        Count(nameof(SetMount));
        player.mount._mountSpecificData = new Data();
        skipDust = true;
    }

    public override void Dismount(Player player, ref bool skipDust) => Count(nameof(Dismount));

    public override void UpdateEffects(Player player)
    {
        Count(nameof(UpdateEffects));
        if (player.mount._mountSpecificData is Data data) data.Ticks++;
    }

    public override bool UpdateFrame(Player mountedPlayer, int state, Vector2 velocity)
    {
        Count(nameof(UpdateFrame));
        return true;
    }

    public override void JumpHeight(Player mountedPlayer, ref int jumpHeight, float xVelocity)
    {
        Count(nameof(JumpHeight));
        JumpHeightSeen = jumpHeight;
    }

    public override bool Draw(List<DrawData> playerDrawData, int drawType, Player drawPlayer, ref Texture2D texture, ref Texture2D glowTexture, ref Vector2 drawPosition, ref Rectangle frame, ref Color drawColor, ref Color glowColor, ref float rotation, ref SpriteEffects spriteEffects, ref Vector2 drawOrigin, ref float drawScale, float shadow)
    {
        Count(nameof(Draw) + drawType);
        return true;
    }
}
