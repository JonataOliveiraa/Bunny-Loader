using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace Terraria.ModLoader;

public abstract class ModSubworld : ModType, ILocalizedModType
{
    public virtual string LocalizationCategory => "Subworlds";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public abstract int Width { get; }

    public abstract int Height { get; }

    public abstract List<GenPass> GenerationTasks { get; }

    public virtual bool Persistent => false;

    public virtual bool SavePlayerChanges => true;

    public virtual bool RandomTileUpdates => false;

    public virtual bool HideUnderworld => true;

    public virtual string LoadingText => Language.GetTextValue("BunnyLoader.LoadingDimension");

    public virtual bool ShowLoadingScreen => true;

    public virtual float TransitionOutSeconds => 0f;

    public virtual float TransitionInSeconds => 0f;

    public string FileName => Mod.Name + "_" + Name;

    protected sealed override void Register()
    {
        ModTypeLookup<ModSubworld>.Register(this);
        SubworldLoader.Register(this);
    }

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void OnEnter() { }

    public virtual void OnExit() { }

    public virtual void PostWorldReady() { }

    public virtual void Update() { }

    public virtual void SaveCarriedData(TagCompound tag) { }

    public virtual void LoadCarriedData(TagCompound tag) { }

    public virtual void DrawTransition(SpriteBatch spriteBatch, float progress) =>
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * progress);
}
