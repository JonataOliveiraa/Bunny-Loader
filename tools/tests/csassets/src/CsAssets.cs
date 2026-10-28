using System;
using System.IO;
using CsAssets.Content.Items;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CsAssets;

public sealed class CsAssetsMod : Mod
{
    public override void Load()
    {
        Asset<Texture2D> gem = Assets.Request<Texture2D>("Content/Items/AssetGem");
        Checks.Report("request in Load", gem.Name == "CsAssets/Content/Items/AssetGem" && gem.State == AssetState.NotLoaded && !gem.IsLoaded,
            $"name={gem.Name} state={gem.State}");
        Checks.Report("same request, same asset",
            ReferenceEquals(gem, Assets.Request<Texture2D>("Content/Items/AssetGem.png")) &&
            ReferenceEquals(gem, ModContent.Request<Texture2D>("CsAssets/Content/Items/AssetGem")), "");
        Checks.Throws<InvalidOperationException>("Value off the game thread", () => _ = gem.Value);
        Checks.Throws<FileNotFoundException>("missing asset", () => Assets.Request<Texture2D>("Content/Items/Nope"));
        Checks.Throws<NotSupportedException>("only Texture2D", () => Assets.Request<Item>("Content/Items/AssetGem"));
        Checks.Throws<System.Collections.Generic.KeyNotFoundException>("unknown mod", () => ModContent.Request<Texture2D>("NoSuchMod/Content/X"));
        Checks.Report("HasAsset", Assets.HasAsset<Texture2D>("Content/Items/AssetGem") && !Assets.HasAsset<Texture2D>("Content/Items/Nope") &&
            ModContent.HasAsset("CsAssets/Content/Items/AssetGem"), "");

        Checks.Report("Find", ModContent.Find<ModItem>("CsAssets/AssetGem") is AssetGem && Find<ModItem>("AssetGem") is AssetGem &&
            ModContent.Find<ModItem>("CsAssets", "AssetGem") is AssetGem, "");
        Checks.Report("TryFind", ModContent.TryFind("CsAssets/AssetGem", out ModItem found) && found is AssetGem &&
            !ModContent.TryFind("CsAssets/Nope", out ModItem _) && !TryFind("Nope", out ModItem _), "");
        Checks.Throws<System.Collections.Generic.KeyNotFoundException>("Find of a missing name", () => ModContent.Find<ModItem>("CsAssets/Nope"));
        Checks.Report("not loaded: GetInstance null, ItemType 0",
            ModContent.GetInstance<UnloadedItem>() == null && ModContent.ItemType<UnloadedItem>() == 0 && ModContent.ItemType<AssetGem>() > 0,
            $"gem={ModContent.ItemType<AssetGem>()}");

        Checks.Report("FileExists", FileExists("Localization/en-US.json") && !FileExists("../manifest.json") && !FileExists("nope.txt") &&
            ModContent.FileExists("CsAssets/Localization/en-US.json"), "");
        byte[] bytes = GetFileBytes("Localization/en-US.json");
        using (Stream stream = GetFileStream("Localization/en-US.json"))
            Checks.Report("GetFileBytes and GetFileStream", bytes.Length > 0 && stream.Length == bytes.Length &&
                ModContent.GetFileBytes("CsAssets/Localization/en-US.json").Length == bytes.Length, $"bytes={bytes.Length}");
        Checks.Throws<FileNotFoundException>("GetFileBytes of a missing file", () => GetFileBytes("nope.txt"));
        Checks.Report("GetFileNames", GetFileNames().Contains("Content/Items/AssetGem.png") && GetFileNames().Contains("Localization/en-US.json"),
            string.Join(",", GetFileNames()));
        (string mod, string rest) = ModContent.SplitName("CsAssets/Content/Items/AssetGem");
        Checks.Report("SplitName", mod == "CsAssets" && rest == "Content/Items/AssetGem", $"{mod} | {rest}");
    }
}
