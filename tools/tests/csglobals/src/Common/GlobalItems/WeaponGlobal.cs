using Terraria;
using Terraria.ModLoader;

namespace CsGlobals.Common.GlobalItems;

public class WeaponGlobal : GlobalItem
{
    public int Hits;

    public int SawDamage;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Item entity, bool lateInstantiation) => lateInstantiation && entity.damage > 0;

    public override void SetDefaults(Item entity) => SawDamage = entity.damage;
}
