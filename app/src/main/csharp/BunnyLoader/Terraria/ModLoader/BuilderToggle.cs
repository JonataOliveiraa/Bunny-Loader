using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class BuilderToggle : ModTexturedType, ILocalizedModType
{
    sealed class VanillaToggle : BuilderToggle
    {
        internal VanillaToggle(int type) { Type = type; }
        public override string DisplayValue() => "";
    }

    public static BuilderToggle TorchBiome { get; } = new VanillaToggle(11);
    public int Type { get; internal set; }
    public virtual string LocalizationCategory => "BuilderToggles";
    public virtual string HoverTexture => "Terraria/Images/UI/InfoIcon_13";
    public virtual bool Active() => false;
    public virtual int NumberOfStates => 2;
    public virtual Position OrderPosition => new Default();
    public abstract string DisplayValue();
    public virtual bool Draw(SpriteBatch batch, ref BuilderToggleDrawParams drawParams) => true;
    public virtual bool DrawHover(SpriteBatch batch, ref BuilderToggleDrawParams drawParams) => true;
    public virtual bool OnLeftClick(ref SoundStyle? sound) => true;
    public virtual void OnRightClick() { }
    public int CurrentState
    {
        get => BuilderToggleHooks.State(this);
        set => BuilderToggleHooks.SetState(this, value);
    }

    protected sealed override void Register() { ModTypeLookup<BuilderToggle>.Register(this); Type = BuilderToggleHooks.Register(this); }
    public sealed override void SetupContent()
    {
        ModContent.Request<Texture2D>(Texture);
        ModContent.Request<Texture2D>(HoverTexture);
        SetStaticDefaults();
    }

    public abstract class Position { }
    public sealed class Default : Position { }
    public sealed class Before : Position { public BuilderToggle Toggle { get; } public Before(BuilderToggle toggle) { Toggle = toggle; } }
    public sealed class After : Position { public BuilderToggle Toggle { get; } public After(BuilderToggle toggle) { Toggle = toggle; } }
}
