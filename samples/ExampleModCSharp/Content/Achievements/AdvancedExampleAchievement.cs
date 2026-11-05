
using ExampleMod.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Achievements;

public class AdvancedExampleAchievement : ModAchievement
{
    
    public override string TextureName => "ExampleMod/Content/Achievements/AllAchievements";
    public override int Index => 1;

    public CustomFlagCondition IronPickaxeCondition;
    public CustomIntCondition TotalDamageCondition;

    public override void SetStaticDefaults() {
        Achievement.SetCategory(AchievementCategory.Challenger);

        IronPickaxeCondition = AddCondition();

        TotalDamageCondition = AddIntCondition("TotalDamage", 100);


        var KillExampleCritterCondition = AddNPCKilledCondition(ModContent.NPCType<NPCs.ExampleCritterNPC>());
        var KillFrogCondition = AddNPCKilledCondition(NPCID.Frog);

        var PickupCondition = AddItemPickupCondition([ModContent.ItemType<NPCs.ExampleCritterItem>(), ItemID.Frog]);

        var CraftCageCondition = AddItemCraftCondition([ModContent.ItemType<ExampleCritterCageItem>(), ItemID.FrogCage]);
        
        ((AchievementCondition)CraftCageCondition).OnComplete += (condition) => Main.NewText($"You are making progress towards '{Achievement.FriendlyName}', keep at it.");

        var MineCageCondition = AddTileDestroyedCondition([ModContent.TileType<ExampleCritterCage>(), TileID.FrogCage]);

        Achievement.UseTracker(new CustomTracker(
            new Dictionary<AchievementCondition, float>() {
                [IronPickaxeCondition] = 1f,
                [TotalDamageCondition] = 2f,

                [KillExampleCritterCondition] = 0.5f,
                [KillFrogCondition] = 0.5f,
                [PickupCondition] = 4f,
                [CraftCageCondition] = 1f,
                [MineCageCondition] = 1f,
            }
        ));
    }

    public override IEnumerable<Position> GetModdedConstraints() {
        yield return new After(ModContent.GetInstance<ManyExampleWormsKilled>());
    }

    public override void OnCompleted(Achievement achievement) {
        
        int fireworkProjectile = ProjectileID.RocketFireworksBoxRed + Main.rand.Next(4);
        Projectile.NewProjectile(Main.LocalPlayer.GetSource_FromThis(), Main.LocalPlayer.Top, new Vector2(Main.rand.NextFloat(-2, 2), -Main.rand.NextFloat(2f, 4f)).RotatedByRandom(0.3f), fireworkProjectile, 0, 0, Main.myPlayer);
    }
}

public sealed class CustomTracker
{
    private readonly Dictionary<AchievementCondition, float> weightedConditions;
    private readonly CustomFloatCondition progress;
    private readonly Terraria.Achievements.IAchievementTracker tracker;

    public CustomTracker(Dictionary<AchievementCondition, float> weightedConditions)
    {
        this.weightedConditions = weightedConditions;
        float maximum = 0f;
        foreach (var entry in weightedConditions) maximum += entry.Value;
        progress = new CustomFloatCondition(CustomFloatCondition.Create("WeightedProgress", maximum).Pointer);
        tracker = ((AchievementCondition)progress).GetAchievementTracker();
        foreach (var entry in weightedConditions) entry.Key.OnComplete += OnConditionCompleted;
        On_ConditionFloatTracker.Load += Load;
        Refresh();
    }

    private void OnConditionCompleted(AchievementCondition condition) => Refresh();

    private void Refresh()
    {
        float value = 0f;
        foreach (var entry in weightedConditions) if (entry.Key.IsCompleted) value += entry.Value;
        progress.Value = value;
    }

    private void Load(On_ConditionFloatTracker.orig_Load orig, ConditionFloatTracker self)
    {
        orig(self);
        if (self.Pointer == tracker.Pointer) Refresh();
    }

    public static implicit operator Terraria.Achievements.IAchievementTracker(CustomTracker value) => value.tracker;
}
