using System;
using System.Collections.Generic;
using Terraria.ModLoader.Core;
using Terraria.WorldBuilding;

namespace Terraria.ModLoader;

public abstract partial class ModSystem
{
    protected override void InitTemplateInstance()
    {
        base.InitTemplateInstance();
        WorldGenHooks.Install(GetType());
    }

    public virtual void PreWorldGen() { }

    public virtual void ModifyWorldGenTasks(List<GenPass> tasks) { }

    public virtual void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        double previousWeight = WorldGenHooks.WeightOf(tasks);
        ModifyWorldGenTasks(tasks);
        totalWeight += WorldGenHooks.WeightOf(tasks) - previousWeight;
    }

    public virtual void PostWorldGen() { }

    public virtual void ModifyHardmodeTasks(List<GenPass> list) { }
}
