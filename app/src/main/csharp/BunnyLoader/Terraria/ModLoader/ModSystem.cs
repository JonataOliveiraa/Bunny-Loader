using System;
using Microsoft.Xna.Framework;
using Terraria.IO;
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
}
