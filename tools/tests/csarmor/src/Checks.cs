using System;
using BunnyLoader.Interop;
using CsArmor.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsArmor;

public static class Checks
{
    const int Slots = 5;

    static int pass, fail, frames;
    static bool done;
    static readonly Item[] Saved = new Item[Slots];
    static readonly GameHandle?[] Pins = new GameHandle?[Slots];

    static ILog Log => ModContent.GetInstance<CsArmorMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done) return;
        frames++;
        if (frames == 120)
        {
            try { Static(); Equip(p); Accessories(p); }
            catch (Exception e) { Report("setup", false, e.ToString()); }
            return;
        }
        if (frames != 240) return;
        done = true;
        try { Worn(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Restore(p); }
        Log.Info($"csarmor END pass={pass} fail={fail}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static void Static()
    {
        Item helmet = Make(ModContent.ItemType<ArmorHelmet>()), body = Make(ModContent.ItemType<ArmorBreastplate>());
        Item legs = Make(ModContent.ItemType<ArmorLeggings>());
        Report("the tModLoader sets keep the vanilla defaults", ArmorIDs.Head.Sets.DrawHatHair[14] && ArmorIDs.Head.Sets.DrawFullHair[10] &&
            !ArmorIDs.Body.Sets.HidesHands[77] && ArmorIDs.Body.Sets.HidesHands[1] && ArmorIDs.Body.Sets.HidesArms[83] &&
            ArmorIDs.Legs.Sets.HidesBottomSkin[20], "");
        Report("the sets cover the mod slots", ArmorIDs.Head.Sets.DrawHatHair.Length >= ArmorIDs.Head.Count && ArmorIDs.Head.Sets.DrawHatHair[helmet.headSlot] &&
            ArmorIDs.Body.Sets.HidesTopSkin[body.bodySlot] && !ArmorIDs.Body.Sets.HidesHands[body.bodySlot] &&
            ArmorIDs.Legs.Sets.HidesBottomSkin[legs.legSlot],
            $"head={helmet.headSlot}/{ArmorIDs.Head.Sets.DrawHatHair.Length} body={body.bodySlot} legs={legs.legSlot}");
    }

    static void Equip(Player p)
    {
        var armor = p.armor;
        for (int i = 0; i < Slots; i++)
        {
            Saved[i] = armor[i];
            Pins[i] = Saved[i].IsNull ? null : new GameHandle(Saved[i].Pointer);
        }
        armor[0] = Make(ModContent.ItemType<ArmorHelmet>());
        armor[1] = Make(ModContent.ItemType<ArmorBreastplate>());
        armor[2] = Make(ModContent.ItemType<ArmorLeggings>());
        armor[3] = Make(0);
        armor[4] = Make(0);
    }

    static void Accessories(Player p)
    {
        var armor = p.armor;
        Item ring = Make(ModContent.ItemType<ArmorRing>());
        int refused = ItemSlot.PickItemMovementAction(armor, ItemSlot.Context.EquipAccessory, 3, ring);
        int slot3 = ArmorRing.LastSlot;
        int allowed = ItemSlot.PickItemMovementAction(armor, ItemSlot.Context.EquipAccessory, 4, ring);
        Report("CanEquipAccessory refuses dropping into a slot", refused == -1 && allowed != -1 && slot3 == 3,
            $"slot3={refused} slot4={allowed} asked={slot3}");

        Item back = ItemSlot.ArmorSwap(ring, out bool success, out int target);
        Report("CanEquipAccessory undoes the quick swap", !success && target == -1 && back.type == ring.type && armor[3].IsAir,
            $"success={success} target={target} back={back.type} slot3={armor[3].type}");
        ArmorRing.LastSlot = -1;
        Report("CanEquipAccessoryInSlot asks the item", !ItemSlot.CanEquipAccessoryInSlot(ring, 3) && ArmorRing.LastSlot == 3, $"asked={ArmorRing.LastSlot}");
    }

    static void Worn(Player p)
    {
        var armor = p.armor;
        Report("UpdateArmorSet sets player.setBonus", p.setBonus == ArmorHelmet.Bonus, $"setBonus='{p.setBonus}'");

        Item helmet = armor[0];
        helmet.wornArmor = true;
        var texts = GameArray<GameString>.New(new GameClass("System", "String"), 64);
        var prefix = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        var bad = GameArray<bool>.New(new GameClass("System", "Boolean"), 64);
        int yoyo = -1, research = -1, materials = -1, count = 1, setBonus = -1, shared = -1;
        Color color = new(255, 255, 255, 255);
        Main.MouseText_DrawItemTooltip_GetLinesInfo(helmet, ref yoyo, ref research, ref materials, helmet.knockBack, ref count, texts, prefix, bad,
            ref setBonus, ref color, ref shared);
        string line = setBonus >= 0 && setBonus < count ? texts[setBonus].ToString() ?? "" : "";
        Report("the worn piece shows the set bonus line", line.Contains(ArmorHelmet.Bonus) && color.G == 205 && color.R == 50,
            $"line={setBonus}/{count} '{line}' color={color.R},{color.G},{color.B}");

        p.GetHairSettings(out bool fullHair, out bool hatHair, out _, out _, out _);
        Report("DrawHatHair reaches GetHairSettings", hatHair && !fullHair && p.head == helmet.headSlot, $"hat={hatHair} full={fullHair} head={p.head}");

        ArmorHelmet.ColorCalls = ArmorBreastplate.ArmCalls = 0;
        PlayerDrawSet set = PlayerDrawSet.New();
        Vector2 position = p.position, origin = Vector2.Zero;
        set.BoringSetup(p, ref position, 0f, 0f, ref origin, default);
        Report("the Hides sets reach the draw setup", set.hidesTopSkin && set.hidesBottomSkin && set.missingHand,
            $"top={set.hidesTopSkin} bottom={set.hidesBottomSkin} hand={set.missingHand}");
        Report("DrawArmorColor sets the head glow mask", ArmorHelmet.ColorCalls > 0 && set.headGlowMask == 1 && set.headGlowColor.R == 255 && set.headGlowColor.G == 0,
            $"calls={ArmorHelmet.ColorCalls} glow={set.headGlowMask} color={set.headGlowColor.R},{set.headGlowColor.G}");
        Report("ArmorArmGlowMask sets the arm glow mask", ArmorBreastplate.ArmCalls > 0 && set.armGlowMask == 2,
            $"calls={ArmorBreastplate.ArmCalls} glow={set.armGlowMask}");
    }

    static void Restore(Player p)
    {
        try
        {
            var armor = p.armor;
            for (int i = 0; i < Slots; i++)
            {
                if (!Saved[i].IsNull) armor[i] = Saved[i];
                Pins[i]?.Dispose();
                Pins[i] = null;
            }
        }
        catch (Exception e) { Report("restore", false, e.Message); }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csarmor {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
