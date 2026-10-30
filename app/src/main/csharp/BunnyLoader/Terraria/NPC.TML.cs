using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct NPC : IEntityWithGlobals<GlobalNPC>
{
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
