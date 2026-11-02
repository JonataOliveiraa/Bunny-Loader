using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader.IO;

namespace Terraria.ModLoader;

public abstract partial class ModSystem : ModType
{
    protected sealed override void Register() => SystemLoader.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void OnModLoad() { }

    public virtual void OnModUnload() { }

    public virtual void PostSetupContent() { }

    public virtual void OnLocalizationsLoaded() { }

    public virtual void ResizeArrays() { }

    public virtual void AddRecipeGroups() { }

    public virtual void AddRecipes() { }

    public virtual void PostAddRecipes() { }

    public virtual void PostSetupRecipes() { }

    public virtual void OnWorldLoad() { }

    public virtual void OnWorldUnload() { }

    public virtual void ClearWorld() { }

    public virtual void PreSaveAndQuit() { }

    public virtual void SaveWorldData(TagCompound tag) { }

    public virtual void LoadWorldData(TagCompound tag) { }

    public virtual void SaveWorldHeader(TagCompound tag) { }

    public virtual bool CanWorldBePlayed(PlayerFileData playerData, WorldFileData worldFileData) => true;

    public virtual string WorldCanBePlayedRejectionMessage(PlayerFileData playerData, WorldFileData worldData) =>
        $"The selected character {playerData.Name} cannot enter the selected world {worldData.Name}";

    public virtual void PreUpdateEntities() { }

    public virtual void PreUpdatePlayers() { }

    public virtual void PostUpdatePlayers() { }

    public virtual void PreUpdateNPCs() { }

    public virtual void PostUpdateNPCs() { }

    public virtual void PreUpdateGores() { }

    public virtual void PostUpdateGores() { }

    public virtual void PreUpdateProjectiles() { }

    public virtual void PostUpdateProjectiles() { }

    public virtual void PreUpdateItems() { }

    public virtual void PostUpdateItems() { }

    public virtual void PreUpdateDusts() { }

    public virtual void PostUpdateDusts() { }

    public virtual void PreUpdateTime() { }

    public virtual void PostUpdateTime() { }

    public virtual void PreUpdateWorld() { }

    public virtual void PostUpdateWorld() { }

    public virtual void PreUpdateInvasions() { }

    public virtual void PostUpdateInvasions() { }

    public virtual void PostUpdateEverything() { }

    public virtual void UpdateUI(GameTime gameTime) { }

    public virtual void PostUpdateInput() { }

    public virtual void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate) { }

    public virtual void ResetNearbyTileEffects() { }

    public virtual void TileCountsAvailable(ReadOnlySpan<int> tileCounts) { }

    public virtual void ModifyScreenPosition() { }

    public virtual void ModifyTransformMatrix(ref SpriteViewMatrix Transform) { }

    public virtual void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) { }

    public virtual void ModifyLightingBrightness(ref float scale) { }

    public virtual void PostDrawTiles() { }

    public virtual void ModifyInterfaceLayers(List<Terraria.UI.GameInterfaceLayer> layers) { }

    public virtual void PostDrawInterface(SpriteBatch spriteBatch) { }

    public virtual void PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext) { }

    public virtual void PostDrawFullscreenMap(ref string mouseText) { }

    public virtual void NetSend(BinaryWriter writer) { }

    public virtual void NetReceive(BinaryReader reader) { }

    public virtual bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber) => false;

    public virtual bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7) => false;
}
