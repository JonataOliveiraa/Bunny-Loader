using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct NPC : IEntityWithGlobals<GlobalNPC>
{
    public unsafe void VanillaAI()
    {
        if (IsNull) throw new NullReferenceException("NPC.VanillaAI: game object is null");
        BunnyLoader.Host.Native()->NPCOriginal(Pointer, 0, 0);
    }

    public void HitEffect(int hitDirection = 0, double dmg = 10, bool? instantKill = null) =>
        HitEffect(NPCEventHooks.LegacyHit(hitDirection, dmg, instantKill));

    public void HitEffect(int hitDirection, double dmg) => HitEffect(hitDirection, dmg, null);

    public void HitEffect(HitInfo hit) => NPCEventHooks.Hit(this, hit);

    public void BecomeImmuneTo(int buffType)
    {
        NPCImmunity.Resize();
        if (buffImmune.Length < BuffLoader.BuffCount) buffImmune = buffImmune.Resized(BuffLoader.BuffCount);
        if ((uint)buffType >= (uint)buffImmune.Length) throw new ArgumentOutOfRangeException(nameof(buffType));
        buffImmune[buffType] = true;
        var inherit = Terraria.ID.BuffID.Sets.GrantImmunityWith;
        for (int buff = 0; buff < inherit.Length; buff++)
            if (inherit[buff].Contains(buffType)) buffImmune[buff] = true;
    }

    public void ClearImmuneToBuffs(out bool anyBuffsCleared)
    {
        anyBuffsCleared = false;
        for (int index = maxBuffs - 1; index >= 0; index--)
        {
            int buff = buffType[index];
            if (buff > 0 && (uint)buff < (uint)buffImmune.Length && buffImmune[buff])
            {
                DelBuff(index, false);
                anyBuffsCleared = true;
            }
        }
        if ((Main.netMode & 2) != 0) NetMessage.SendData(54, -1, -1, default, whoAmI);
    }

    public ModNPC? ModNPC => NPCLoader.InstanceOf(this);

    public IReadOnlyList<GlobalNPC> EntityGlobals => NPCLoader.Globals.Of(this);

    public T GetGlobalNPC<T>() where T : GlobalNPC => NPCLoader.Globals.Get<T>(this);

    public T GetGlobalNPC<T>(T baseInstance) where T : GlobalNPC => NPCLoader.Globals.Get(this, baseInstance);

    public bool TryGetGlobalNPC<T>(out T result) where T : GlobalNPC => NPCLoader.Globals.TryGet(this, out result);

    public bool TryGetGlobalNPC<T>(T baseInstance, out T result) where T : GlobalNPC =>
        NPCLoader.Globals.TryGet(this, baseInstance.StaticIndex, out result);

    public void CloneDefaults(int Type) => NPCDefaultsHooks.CloneDefaults(this, Type);

    public void SetDefaults(int Type) => SetDefaults(Type, default);

    public void SetDefaultsKeepPlayerInteraction(int Type) => SetDefaultsKeepPlayerInteraction(Type, default);

    public void SetDefaultsKeepPlayerInteraction(int Type, NPCSpawnParams spawnParams)
    {
        bool[] saved = playerInteraction.AsSpan().ToArray();
        try { SetDefaults(Type, spawnParams); }
        finally { saved.AsSpan().CopyTo(playerInteraction.AsSpan()); }
    }
}
