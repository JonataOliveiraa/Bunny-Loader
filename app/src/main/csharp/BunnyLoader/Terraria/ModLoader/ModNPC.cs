using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModNPC : ModType<NPC, ModNPC>, ILocalizedModType
{
    public ref NPC NPC => ref entity;

    public int Type { get; internal set; }

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

    public virtual void AutoStaticDefaults() =>
        TextureAssets.Npc[Type] = new Asset<Texture2D>(Texture, ItemLoader.ResolveTexture(this, Texture));
}
