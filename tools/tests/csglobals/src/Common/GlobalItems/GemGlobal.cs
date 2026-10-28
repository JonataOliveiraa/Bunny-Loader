using CsGlobals.Content.Items;
using Terraria;
using Terraria.ModLoader;

namespace CsGlobals.Common.GlobalItems;

public class GemGlobal : GlobalItem
{
    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
        !lateInstantiation && entity.type == ModContent.ItemType<GlobalGem>();

    public override void SetDefaults(Item entity) => GlobalGem.Order.Add("GemGlobal.SetDefaults");
}
