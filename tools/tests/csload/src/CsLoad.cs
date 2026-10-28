using System;
using System.Collections.Generic;
using System.Linq;
using CsLoad.Content;
using CsLoad.Content.Items;
using Terraria.ModLoader;

namespace CsLoad;

public sealed class CsLoadMod : Mod
{
    public override void Load()
    {
        Checks.Note("Mod.Load");
        Checks.Report("autoload before Load", Checks.Events.Contains("OrderItem.Load") && Checks.Events.Contains("PlainLoadable.Load"),
            string.Join(",", Checks.Events));
        Checks.Report("manual AddContent", AddContent<ManualItem>() && ModContent.ItemType<ManualItem>() > 0,
            $"type={ModContent.ItemType<ManualItem>()}");
        Checks.Report("GetInstance of the mod", ModContent.GetInstance<CsLoadMod>() == this, "");
    }

    public override void PostSetupContent()
    {
        Checks.Note("Mod.PostSetupContent");
        Checks.Report("left out", Missing<DisabledItem>() && Missing<ServerItem>() && Missing<HiddenBase>() && Missing<HiddenChild>() &&
            !Checks.Events.Contains("DisabledItem.Load"), string.Join(",", Checks.Events));
        string[] items = GetContent<ModItem>().Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Checks.Report("GetContent<ModItem>", items.SequenceEqual(new[] { "CloneItem", "ManualItem", "OrderItem" }), string.Join(",", items));
        Checks.Report("GetInstance of an ILoadable", ModContent.GetInstance<PlainLoadable>().Loaded == this, "");
        try
        {
            AddContent(new PlainLoadable());
            Checks.Report("AddContent after load throws", false, "no exception");
        }
        catch (InvalidOperationException)
        {
            Checks.Report("AddContent after load throws", true, "");
        }
        Checks.RunInstances();
    }

    public override void AddRecipeGroups() => Checks.Note("Mod.AddRecipeGroups");

    public override void AddRecipes() => Checks.Note("Mod.AddRecipes");

    public override void PostAddRecipes()
    {
        Checks.Note("Mod.PostAddRecipes");
        var expected = new List<string>
        {
            "OrderItem.Load", "PlainLoadable.Load", "Mod.Load", "OrderItem.SetStaticDefaults", "Mod.PostSetupContent",
            "Mod.AddRecipeGroups", "Mod.AddRecipes", "OrderItem.AddRecipes", "Mod.PostAddRecipes",
        };
        List<string> got = Checks.Events.Where(expected.Contains).ToList();
        Checks.Report("order", got.SequenceEqual(expected), string.Join(",", got));
        Checks.Log.Info($"csload END pass={Checks.Pass} fail={Checks.Fail}");
    }

    static bool Missing<T>() where T : class => ModContent.GetInstance<T>() == null;
}
