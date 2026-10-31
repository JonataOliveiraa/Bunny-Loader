using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public enum MountTextureType
{
    Back,
    BackGlow,
    BackExtra,
    BackExtraGlow,
    Front,
    FrontGlow,
    FrontExtra,
    FrontExtraGlow,
}

public abstract class ModMount : ModType
{
    Mount.MountData mountData;

    public ref Mount.MountData MountData => ref mountData;

    public int Type { get; internal set; }

    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

    protected sealed override void Register() => Type = MountLoader.Register(this);

    public sealed override void SetupContent()
    {
        mountData = Mount.MountData.New();
        foreach (MountTextureType textureType in Enum.GetValues<MountTextureType>())
        {
            string extraTexture = GetExtraTexture(textureType);
            if (string.IsNullOrEmpty(extraTexture) || !ModContent.RequestIfExists<Texture2D>(extraTexture, out Asset<Texture2D> asset)) continue;
            ref Mount.MountData data = ref mountData;
            switch (textureType)
            {
                case MountTextureType.Back: data.backTexture = asset; break;
                case MountTextureType.BackGlow: data.backTextureGlow = asset; break;
                case MountTextureType.BackExtra: data.backTextureExtra = asset; break;
                case MountTextureType.BackExtraGlow: data.backTextureExtraGlow = asset; break;
                case MountTextureType.Front: data.frontTexture = asset; break;
                case MountTextureType.FrontGlow: data.frontTextureGlow = asset; break;
                case MountTextureType.FrontExtra: data.frontTextureExtra = asset; break;
                case MountTextureType.FrontExtraGlow: data.frontTextureExtraGlow = asset; break;
            }
        }
        Mount.mounts[Type] = MountData;
        SetStaticDefaults();
    }

    protected virtual string GetExtraTexture(MountTextureType textureType) => Texture + "_" + textureType;

    public virtual void JumpHeight(Player mountedPlayer, ref int jumpHeight, float xVelocity) { }

    public virtual void JumpSpeed(Player mountedPlayer, ref float jumpSeed, float xVelocity) { }

    public virtual void UpdateEffects(Player player) { }

    public virtual bool UpdateFrame(Player mountedPlayer, int state, Vector2 velocity) => true;

    public virtual void UseAbility(Player player, Vector2 mousePosition, bool toggleOn) { }

    public virtual void AimAbility(Player player, Vector2 mousePosition) { }

    public virtual void SetMount(Player player, ref bool skipDust) { }

    public virtual void Dismount(Player player, ref bool skipDust) { }

    public virtual bool Draw(List<DrawData> playerDrawData, int drawType, Player drawPlayer, ref Texture2D texture, ref Texture2D glowTexture, ref Vector2 drawPosition, ref Rectangle frame, ref Color drawColor, ref Color glowColor, ref float rotation, ref SpriteEffects spriteEffects, ref Vector2 drawOrigin, ref float drawScale, float shadow) => true;
}
