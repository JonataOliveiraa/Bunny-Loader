using System;
using System.Collections.Generic;
using System.Linq;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.Localization;

namespace Terraria.ModLoader;

[Autoload(Side = ModSide.Client)]
public abstract class ModAchievement : ModType<Achievement, ModAchievement>, ILocalizedModType
{
    internal readonly List<AchievementCondition> conditions = new();

    public Achievement Achievement => Entity;

    public virtual string TextureName => (GetType().Namespace + "." + Name).Replace('.', '/');

    public Asset<Texture2D> Texture { get; private set; } = null!;

    public virtual int Index => 0;

    public string LocalizationCategory => "Achievements";

    public virtual bool Hidden => false;

    public virtual LocalizedText FriendlyName => this.GetLocalization(nameof(FriendlyName), PrettyPrintName);

    public virtual LocalizedText Description => this.GetLocalization(nameof(Description));

    static LocalUser User => LocalUser.Active.IsNull ? Main.Achievements.User : LocalUser.Active;

    static GameArray<short> Shorts(int[] ids) => GameArrayBuilder.Create<short>(ids.Select(x => (short)x).ToArray());

    public CustomFlagCondition AddCondition(string key = "Condition") => (CustomFlagCondition)Add(CustomFlagCondition.Create(key), null);

    public CustomIntCondition AddIntCondition(int maxValue) => AddIntCondition("Condition", maxValue);

    public CustomIntCondition AddIntCondition(string key, int maxValue) => (CustomIntCondition)Add(CustomIntCondition.Create(key, maxValue), null);

    public CustomFloatCondition AddFloatCondition(float maxValue) => AddFloatCondition("Condition", maxValue);

    public CustomFloatCondition AddFloatCondition(string key, float maxValue) => (CustomFloatCondition)Add(CustomFloatCondition.Create(key, maxValue), null);

    public ItemCraftCondition AddItemCraftCondition(int itemId) => (ItemCraftCondition)Add(ItemCraftCondition.Create(User, (short)itemId), null);

    public ItemCraftCondition AddItemCraftCondition(string key, int itemId) => (ItemCraftCondition)Add(ItemCraftCondition.Create(User, (short)itemId), key);

    public ItemCraftCondition AddItemCraftCondition(int[] itemIds) => (ItemCraftCondition)Add(ItemCraftCondition.Create(User, Shorts(itemIds)), null);

    public ItemCraftCondition AddItemCraftCondition(string key, int[] itemIds) => (ItemCraftCondition)Add(ItemCraftCondition.Create(User, Shorts(itemIds)), key);

    public ItemCraftCondition[] AddManyItemCraftCondition(int[] itemIds) => itemIds.Select(AddItemCraftCondition).ToArray();

    public ItemPickupCondition AddItemPickupCondition(int itemId) => (ItemPickupCondition)Add(ItemPickupCondition.Create(User, (short)itemId), null);

    public ItemPickupCondition AddItemPickupCondition(string key, int itemId) => (ItemPickupCondition)Add(ItemPickupCondition.Create(User, (short)itemId), key);

    public ItemPickupCondition AddItemPickupCondition(int[] itemIds) => (ItemPickupCondition)Add(ItemPickupCondition.Create(User, Shorts(itemIds)), null);

    public ItemPickupCondition AddItemPickupCondition(string key, int[] itemIds) => (ItemPickupCondition)Add(ItemPickupCondition.Create(User, Shorts(itemIds)), key);

    public ItemPickupCondition[] AddManyItemPickupCondition(int[] itemIds) => itemIds.Select(AddItemPickupCondition).ToArray();

    public NPCKilledCondition AddNPCKilledCondition(int npcID) => (NPCKilledCondition)Add(NPCKilledCondition.Create(User, (short)npcID), null);

    public NPCKilledCondition AddNPCKilledCondition(string key, int npcID) => (NPCKilledCondition)Add(NPCKilledCondition.Create(User, (short)npcID), key);

    public NPCKilledCondition AddNPCKilledCondition(int[] npcIDs) => (NPCKilledCondition)Add(NPCKilledCondition.Create(User, Shorts(npcIDs)), null);

    public NPCKilledCondition AddNPCKilledCondition(string key, int[] npcIDs) => (NPCKilledCondition)Add(NPCKilledCondition.Create(User, Shorts(npcIDs)), key);

    public NPCKilledCondition[] AddManyNPCKilledCondition(int[] npcIDs) => npcIDs.Select(AddNPCKilledCondition).ToArray();

    public TileDestroyedCondition AddTileDestroyedCondition(int[] tileIds) => AddTileDestroyedCondition(null!, tileIds);

    public TileDestroyedCondition AddTileDestroyedCondition(string key, int[] tileIds) =>
        (TileDestroyedCondition)Add(TileDestroyedCondition.Create(User, GameArrayBuilder.Create<ushort>(tileIds.Select(x => (ushort)x).ToArray())), key);

    public T AddCondition<T>(T condition) where T : struct, IGameObject
    {
        Add(new AchievementCondition(condition.Pointer), null);
        return condition;
    }

    AchievementCondition Add(AchievementCondition condition, string? key)
    {
        if (Achievement.IsNull)
            throw new InvalidOperationException($"achievement {Name}: conditions can only be added in SetStaticDefaults");
        if (key != null)
            condition.Name = key;
        Achievement.AddCondition(condition);
        conditions.Add(condition);
        return condition;
    }

    protected sealed override void Register()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Achievement name cannot be null or empty.");
        AchievementLoader.Add(this);
    }

    public virtual void OnCompleted(Achievement achievement)
    {
    }

    public sealed override void SetupContent()
    {
        Entity = Achievement.New(FullName);
        if (Achievement.Category == AchievementCategory.None)
            Achievement.SetCategory(AchievementCategory.Slayer);
        Achievement.FriendlyName = FriendlyName;
        Achievement.Description = Description;
        Texture = ModContent.Request<Texture2D>(TextureName);
        SetStaticDefaults();
        AutoStaticDefaults();
        AchievementLoader.Setup(this);
    }

    protected sealed override Achievement CreateTemplateEntity() => default;

    public virtual void AutoStaticDefaults()
    {
        if (Achievement.HasTracker)
            return;
        if (conditions.Count > 1)
        {
            Achievement.UseConditionsCompletedTracker();
        }
        else if (conditions.Count == 1)
        {
            IAchievementTracker tracker = conditions[0].GetAchievementTracker();
            if (!tracker.IsNull)
                Achievement.UseTracker(tracker);
        }
    }

    public virtual Position GetDefaultPosition() => AfterLastVanillaAchievement;

    public virtual Position? GetAdvisorPosition() => null;

    public virtual IEnumerable<Position>? GetModdedConstraints() => null;

    public abstract class Position { }

    public static Position BeforeFirstVanillaAchievement => new Before("TIMBER");

    public static Position AfterLastVanillaAchievement => new Default();

    public static Position BeforeFirstAdvisorAchievement => new Default();

    public static Position AfterLastAdvisorAchievement => new Default();

    public sealed class Default : Position { }

    public sealed class Before : Position
    {
        public Achievement Achievement { get; }

        public Before(Achievement achievement) => Achievement = achievement;

        public Before(ModAchievement modAchievement) => Achievement = modAchievement.Achievement;

        public Before(string achievementIdentifier) => Achievement = Main.Achievements.GetAchievement(achievementIdentifier);
    }

    public sealed class After : Position
    {
        public Achievement Achievement { get; }

        public After(Achievement achievement) => Achievement = achievement;

        public After(ModAchievement modAchievement) => Achievement = modAchievement.Achievement;

        public After(string achievementIdentifier) => Achievement = Main.Achievements.GetAchievement(achievementIdentifier);
    }
}
