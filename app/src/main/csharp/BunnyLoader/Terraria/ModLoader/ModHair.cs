using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace Terraria.ModLoader;

public abstract class ModHair : ModTexturedType
{
    public int Type { get; internal set; }

    public virtual string AltTexture => Texture + "_Alt";

    public virtual Gender RandomizedCharacterCreationGender => Gender.Unspecified;

    public virtual bool AvailableDuringCharacterCreation => true;

    protected sealed override void Register() => Type = HairLoader.Register(this);

    public sealed override void SetupContent()
    {
        AutoStaticDefaults();
        SetStaticDefaults();
    }

    public virtual void AutoStaticDefaults()
    {
        TextureAssets.PlayerHair[Type] = ModContent.Request<Texture2D>(Texture);
        TextureAssets.PlayerHairAlt[Type] = ModContent.Request<Texture2D>(AltTexture);
    }

    public virtual IEnumerable<Condition> GetUnlockConditions() => Enumerable.Empty<Condition>();
}
