using System;
using CsEquip.Content.Items;
using log4net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsEquip;

public static class Checks
{
    const int Slots = 5;

    static int pass, fail, frames;
    static bool done;
    static readonly Item[] Saved = new Item[Slots];
    static readonly BunnyLoader.Interop.GameHandle?[] Pins = new BunnyLoader.Interop.GameHandle?[Slots];

    static ILog Log => ModContent.GetInstance<CsEquipMod>().Logger;

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
            try { Static(); Equip(p); }
            catch (Exception e) { Report("setup", false, e.ToString()); }
            return;
        }
        if (frames != 240) return;
        done = true;
        try { Worn(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Restore(p); }
        Log.Info($"csequip END pass={pass} fail={fail}");
    }

    static Item Make(int type)
    {
        Item item = Item.New();
        item.SetDefaults(type, default);
        return item;
    }

    static void Static()
    {
        Item helmet = Make(ModContent.ItemType<TestHelmet>()), body = Make(ModContent.ItemType<TestBreastplate>());
        Item legs = Make(ModContent.ItemType<TestLeggings>()), wings = Make(ModContent.ItemType<TestWings>()), shield = Make(ModContent.ItemType<TestShield>());
        Report("AutoloadEquip gives each item a slot after the game's",
            helmet.headSlot > 0 && helmet.headSlot < ArmorIDs.Head.Count && Item.headType[helmet.headSlot] == helmet.type &&
            body.bodySlot > 0 && Item.bodyType[body.bodySlot] == body.type && legs.legSlot > 0 && Item.legType[legs.legSlot] == legs.type &&
            wings.wingSlot > 0 && shield.shieldSlot > 0,
            $"head={helmet.headSlot}/{ArmorIDs.Head.Count} body={body.bodySlot}/{ArmorIDs.Body.Count} legs={legs.legSlot}/{ArmorIDs.Legs.Count} wings={wings.wingSlot}/{ArmorIDs.Wing.Count} shield={shield.shieldSlot}/{ArmorIDs.Shield.Count}");
        Report("the textures go into TextureAssets", !TextureAssets.ArmorHead[helmet.headSlot].IsNull && !TextureAssets.ArmorBodyComposite[body.bodySlot].IsNull &&
            !TextureAssets.ArmorLeg[legs.legSlot].IsNull && !TextureAssets.Wings[wings.wingSlot].IsNull && !TextureAssets.AccShield[shield.shieldSlot].IsNull &&
            ArmorIDs.Body.Sets.UsesNewFramingCode[body.bodySlot], "");
        Report("SetStaticDefaults sees Item.wingSlot and sets WingStats", TestWings.SlotInStatic == wings.wingSlot && ArmorIDs.Wing.Sets.Stats[wings.wingSlot].FlyTime == 180,
            $"static={TestWings.SlotInStatic} flyTime={ArmorIDs.Wing.Sets.Stats[wings.wingSlot].FlyTime}");
        var mod = ModContent.GetInstance<CsEquipMod>();
        int cape = EquipLoader.GetEquipSlot(mod, "Cape", EquipType.Back);
        Report("AddEquipTexture registers by name", cape > 0 && EquipLoader.GetEquipTexture(EquipType.Back, cape)?.Name == $"{mod.Name}/Cape" &&
            !TextureAssets.AccBack[cape].IsNull, $"slot={cape}");

        Item charm = Make(ModContent.ItemType<TestCharm>()), band = Make(ItemID.BandofRegeneration);
        Report("CanAccessoryBeEquippedWith refuses a pair", !ItemSlot.CanEquipBothAccessories(shield, charm, false) &&
            !ItemSlot.CanEquipBothAccessories(charm, shield, false) && ItemSlot.CanEquipBothAccessories(charm, band, false), "");
    }

    static void Equip(Player p)
    {
        var armor = p.armor;
        for (int i = 0; i < Slots; i++)
        {
            Saved[i] = armor[i];
            Pins[i] = Saved[i].IsNull ? null : new BunnyLoader.Interop.GameHandle(Saved[i].Pointer);
        }
        armor[0] = Make(ModContent.ItemType<TestHelmet>());
        armor[1] = Make(ModContent.ItemType<TestBreastplate>());
        armor[2] = Make(ModContent.ItemType<TestLeggings>());
        armor[3] = Make(ModContent.ItemType<TestWings>());
        armor[4] = Make(ModContent.ItemType<TestShield>());
        TestHelmet.SetUpdates = TestHelmet.VanityUpdates = TestBreastplate.Updates = TestShield.Updates = 0;
    }

    static void Worn(Player p)
    {
        var armor = p.armor;
        Report("UpdateEquip runs for worn armor", TestBreastplate.Updates > 0, $"calls={TestBreastplate.Updates}");
        Report("UpdateAccessory runs for an equipped accessory", TestShield.Updates > 0, $"calls={TestShield.Updates} hidden={TestShield.LastHidden}");
        Report("IsArmorSet and UpdateArmorSet run with the full set", TestHelmet.SetUpdates > 0, $"calls={TestHelmet.SetUpdates}");
        Report("the player draws the mod slots", p.head == armor[0].headSlot && p.body == armor[1].bodySlot && p.legs == armor[2].legSlot &&
            p.wings == armor[3].wingSlot && p.shield == armor[4].shieldSlot,
            $"head={p.head}/{armor[0].headSlot} body={p.body}/{armor[1].bodySlot} legs={p.legs}/{armor[2].legSlot} wings={p.wings}/{armor[3].wingSlot} shield={p.shield}/{armor[4].shieldSlot}");
        Report("IsVanitySet comes from IsArmorSet by the drawn slots", TestHelmet.VanityUpdates > 0, $"calls={TestHelmet.VanityUpdates}");

        Vector2 velocity = p.velocity;
        float wingTime = p.wingTime;
        TestWings.Vertical = TestWings.Horizontal = 0;
        p.wingsLogic = armor[3].wingSlot;
        p.WingMovement();
        p.WingAirLogicTweaks();
        p.velocity = velocity;
        p.wingTime = wingTime;
        Report("VerticalWingSpeeds and HorizontalWingSpeeds run for the worn wings", TestWings.Vertical == 1 && TestWings.Horizontal == 1,
            $"vertical={TestWings.Vertical} horizontal={TestWings.Horizontal} logic={p.wingsLogic}");
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
        Log.Info($"csequip {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
