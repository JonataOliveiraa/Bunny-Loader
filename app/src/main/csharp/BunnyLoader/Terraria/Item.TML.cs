using System.Collections.Generic;
using Terraria.ModLoader.Core;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Item : IEntityWithGlobals<GlobalItem>
{
    public ModItem? ModItem => ItemLoader.InstanceOf(this);

    public IReadOnlyList<GlobalItem> EntityGlobals => ItemLoader.Globals.Of(this);

    public T GetGlobalItem<T>() where T : GlobalItem => ItemLoader.Globals.Get<T>(this);

    public T GetGlobalItem<T>(T baseInstance) where T : GlobalItem => ItemLoader.Globals.Get(this, baseInstance);

    public bool TryGetGlobalItem<T>(out T result) where T : GlobalItem => ItemLoader.Globals.TryGet(this, out result);

    public WorldItem GetWorldItem() => ItemPickupHooks.WorldItemOf(this);

    public DamageClass DamageType
    {
        get => DamageClassLoader.ItemClass(this);
        set => DamageClassLoader.SetItemClass(this, value);
    }

    public bool CountsAsClass<T>() where T : DamageClass => CountsAsClass(ModContent.GetInstance<T>());

    public bool CountsAsClass(DamageClass damageClass) => DamageType.CountsAsClass(damageClass);

    public Item(int setDefaultsToType, int stack = 1, int prefix = 0)
    {
        Item item = New();
        ItemLoader.KeepAlive(item);
        item.SetDefaults(setDefaultsToType, default);
        if (item.type > 0)
        {
            item.stack = stack;
            if (prefix > 0) item.Prefix(prefix);
        }
        Pointer = item.Pointer;
    }

    public void CloneDefaults(int TypeToClone) => ItemLoader.CloneDefaults(this, TypeToClone);

    public int ResearchUnlockCount
    {
        get => ItemLoader.ResearchUnlockCount(type);
        set => ItemLoader.SetResearchUnlockCount(type, value);
    }
}
