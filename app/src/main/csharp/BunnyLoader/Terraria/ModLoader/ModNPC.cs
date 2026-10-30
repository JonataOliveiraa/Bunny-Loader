using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class ModNPC : ModType<NPC, ModNPC>, ILocalizedModType
{
    public ref NPC NPC => ref entity;

    int registeredType, aiType, animationType;

    internal NPCMethod Hooks;
    internal int RegisteredType => registeredType;

    public int Type { get => entity.IsNull ? registeredType : entity.type; internal set => registeredType = value; }

    public int AIType
    {
        get => aiType;
        set { aiType = value; if (value > 0 && Type >= Terraria.ID.NPCID.Count) NPCEventHooks.Want(Type, 1); }
    }

    public int AnimationType
    {
        get => animationType;
        set { animationType = value; if (value > 0 && Type >= Terraria.ID.NPCID.Count) NPCEventHooks.Want(Type, 2); }
    }

    public virtual string LocalizationCategory => "NPCs";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

    protected sealed override NPC CreateTemplateEntity() => default;

    protected sealed override void Register() => NPCLoader.Register(this);

    public sealed override void SetupContent()
    {
        AutoStaticDefaults();
        SetStaticDefaults();
        if (!Terraria.ID.NPCID.Search.ContainsName(FullName)) Terraria.ID.NPCID.Search.Add(FullName, Type);
    }

    public virtual void SetDefaults() { }

    public virtual void OnSpawn(IEntitySource source) { }

    public virtual void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) { }

    public virtual void ResetEffects() { }

    public virtual bool PreAI() => true;

    public virtual void AI() { }

    public virtual void PostAI() { }

    public virtual void FindFrame(int frameHeight) { }

    public virtual void HitEffect(NPC.HitInfo hit) { }

    public virtual void UpdateLifeRegen(ref int damage) { }

    public virtual bool CheckActive() => true;

    public virtual bool CheckDead() => true;

    public virtual void AutoStaticDefaults() =>
        TextureAssets.Npc[Type] = new Asset<Texture2D>(Texture, ItemLoader.ResolveTexture(this, Texture));
}
