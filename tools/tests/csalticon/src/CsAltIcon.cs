using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Control = QuickActionButton_Layout.QuickActionControl;

namespace CsAltIcon;

public class IconWand : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.WandofSparking}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 28;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 20;
        Item.damage = 10;
        Item.DamageType = DamageClass.Melee;
    }

    public override bool AltFunctionUse(Player player) => true;
}

public class CustomIconWand : IconWand
{
    public static Asset<Texture2D> Normal => ModContent.Request<Texture2D>($"Terraria/Images/Item_{ItemID.ManaCrystal}");

    public static Asset<Texture2D> Alternative => ModContent.Request<Texture2D>($"Terraria/Images/Item_{ItemID.LifeCrystal}");

    public override Asset<Texture2D> AltFunctionIcon(Player player, bool alternative) => alternative ? Alternative : Normal;
}

public sealed class CsAltIconMod : Mod
{
    const float InteractX = 69f, InteractY = 420f;

    enum Near { None, Npc, WorkBench, Chest }

    record Stage(string Name, int Type, bool? Mode, bool Tap = false, Near Near = Near.None);

    static int frame, step, waited, taps, slot, stage, pass, fail;
    static Item kept;
    static Vector2 home;
    static Point tile;
    static VirtualControlsProfile_Layout profile, advanced;
    static Stage[] stages = Array.Empty<Stage>();
    static bool capturing, swapDrawn, modeBefore, interactable;
    static string swapId = "-";
    static nint swapTexture;
    static int current = -1;
    static (bool Drawn, string Id) vanillaNearNpc;
    static readonly Dictionary<Near, string> vanillaTap = new();

    static ILog Log => ModContent.GetInstance<CsAltIconMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_GUIQuickActionButton.Draw_QuickActionControl_QuickActionButton_Layout_bool_reffloat_bool_bool += Draw;
        On_GUIQuickActionButton.DrawOverloadedButtonStyle_TransactionButton_Layout_Item_QuickActionButton_Layout_bool_bool_reffloat += Overloaded;
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csalticon {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Draw(On_GUIQuickActionButton.orig_Draw_QuickActionControl_QuickActionButton_Layout_bool_reffloat_bool_bool orig, Control actionType, QuickActionButton_Layout layout, bool pressed, ref float scale, bool ignoreChecks, bool ignoreIcon)
    {
        int outer = current;
        current = (int)actionType;
        try { orig(actionType, layout, pressed, ref scale, ignoreChecks, ignoreIcon); }
        finally { current = outer; }
    }

    static void Overloaded(On_GUIQuickActionButton.orig_DrawOverloadedButtonStyle_TransactionButton_Layout_Item_QuickActionButton_Layout_bool_bool_reffloat orig, TransactionButton_Layout overloadedButton, Item drawItem, QuickActionButton_Layout layout, bool disabled, bool pressed, ref float scale)
    {
        orig(overloadedButton, drawItem, layout, disabled, pressed, ref scale);
        if (!capturing || current != (int)Control.Interact || overloadedButton.IsNull) return;
        Texture_Layout icon = overloadedButton.IconTexture;
        swapDrawn = true;
        swapId = icon.IsNull ? "-" : icon.TextureId ?? "-";
        swapTexture = icon.IsNull ? 0 : icon.Texture.Pointer;
    }

    static VirtualControllerInputState Input => GUIInstance.Active.VirtualControllerInputState;

    static void SetMode(bool on)
    {
        VirtualControllerInputState input = Input;
        input.ToggleAlternativeFireMode = on;
    }

    static void Advanced()
    {
        if (!advanced.IsNull && VirtualControlsStyles_Layout.Instance.ActiveProfile.Pointer != advanced.Pointer)
            VirtualControlsStyles_Layout.Instance.SetActiveProfile(advanced);
    }

    static Vector2 Spot(Player self, Near near)
    {
        Vector2 spot = home + new Vector2(-640f, 0f);
        if (near == Near.Npc)
        {
            NPC npc = Main.npc.AsSpan().ToArray().FirstOrDefault(n => !n.IsNull && n.active && n.townNPC);
            if (!npc.IsNull) spot = npc.position + new Vector2(24f, npc.height - self.height);
        }
        return spot;
    }

    static void Place(Player self, Near near)
    {
        self.position = Spot(self, near);
        self.velocity = Vector2.Zero;
    }

    static void PlaceTile(Player self, Near near)
    {
        if (near is not (Near.WorkBench or Near.Chest)) return;
        Vector2 spot = Spot(self, near);
        tile = new Point((int)(spot.X / 16f) + 3, (int)((spot.Y + self.height) / 16f) - 1);
        bool placed = WorldGen.PlaceTile(tile.X, tile.Y, near == Near.WorkBench ? TileID.WorkBenches : TileID.Containers, mute: true);
        Log.Info($"csalticon placed {near} at {tile} {placed} type={Main.tile[tile.X, tile.Y].TileType}");
    }

    static void Clean(Player self, Near near)
    {
        if (self.talkNPC != -1) self.SetTalkNPC(-1);
        if (self.chest != -1) self.chest = -1;
        Main.playerInventory = false;
        if (near is not (Near.WorkBench or Near.Chest)) return;
        for (int x = tile.X - 2; x <= tile.X + 2; x++)
            for (int y = tile.Y - 2; y <= tile.Y; y++)
                if (Main.tile[x, y].HasTile) WorldGen.KillTile(x, y, noItem: true);
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer) return;
        self.creativeGodMode = true;
        frame++;
        try { Step(self); }
        catch (Exception e)
        {
            Report("frame " + frame, false, e.ToString());
            Finish(self);
        }
    }

    static void Step(Player self)
    {
        switch (step)
        {
            case 0 when frame == 30:
            {
                Main.playerInventory = false;
                slot = self.selectedItem;
                kept = self.inventory[slot].Clone();
                home = self.position;
                VirtualControlsStyles_Layout styles = VirtualControlsStyles_Layout.Instance;
                profile = styles.ActiveProfile;
                foreach (VirtualControlsProfile_Layout candidate in styles.Profiles.AsSpan().ToArray())
                    if (!candidate.IsNull && candidate.Id == "Advanced") advanced = candidate;
                int plain = ModContent.ItemType<IconWand>(), custom = ModContent.ItemType<CustomIconWand>(), staff = ItemID.MonkStaffT3;
                stages = new Stage[]
                {
                    new("vanilla", staff, false),
                    new("default", plain, false),
                    new("custom off", custom, false),
                    new("custom on", custom, true),
                    new("vanilla near npc", staff, false, Near: Near.Npc),
                    new("mod near npc", plain, false, Near: Near.Npc),
                    new("vanilla tap", staff, false, Tap: true),
                    new("tap", plain, false, Tap: true),
                    new("vanilla tap near npc", staff, false, true, Near.Npc),
                    new("tap near npc", plain, false, true, Near.Npc),
                    new("vanilla tap near workbench", staff, false, true, Near.WorkBench),
                    new("tap near workbench", plain, false, true, Near.WorkBench),
                    new("vanilla tap near chest", staff, false, true, Near.Chest),
                    new("tap near chest", plain, false, true, Near.Chest),
                };
                step = 1;
                waited = frame;
                break;
            }
            case 1 when frame >= waited + 30:
            {
                if (stage >= stages.Length)
                {
                    Finish(self);
                    break;
                }
                Advanced();
                Place(self, stages[stage].Near);
                PlaceTile(self, stages[stage].Near);
                self.inventory[slot] = new Item(stages[stage].Type);
                waited = frame;
                step = 2;
                break;
            }
            case 2 when frame == waited + 60:
                if (stages[stage].Mode is bool on) SetMode(on);
                waited = frame;
                step = 3;
                break;
            case 3 when frame == waited + 30:
                Advanced();
                Place(self, stages[stage].Near);
                swapDrawn = false;
                swapId = "-";
                swapTexture = 0;
                capturing = true;
                modeBefore = Input.ToggleAlternativeFireMode;
                interactable = Main.HasInteractableObjectThatIsNotATile;
                Log.Info(stages[stage].Tap ? $"csalticon TAP {++taps} {InteractX} {InteractY}" : $"csalticon SHOT {++taps} {stages[stage].Name}");
                waited = frame;
                step = 4;
                break;
            case 4:
                Place(self, stages[stage].Near);
                if (frame < waited + 480) break;
                capturing = false;
                Check(self, stages[stage]);
                Clean(self, stages[stage].Near);
                stage++;
                waited = frame;
                step = 1;
                break;
        }
    }

    static string TapResult(Player self) =>
        $"toggled={Input.ToggleAlternativeFireMode != modeBefore} talk={self.talkNPC != -1} chest={self.chest != -1} inventory={Main.playerInventory}";

    static void Check(Player self, Stage s)
    {
        string seen = $"drawn={swapDrawn} id={swapId} texture={swapTexture:x}";
        switch (s.Name)
        {
            case "vanilla":
                Report("the game draws its swap icon on Interact for its own alternative item", swapDrawn && swapId.EndsWith("ItemSwap1"), seen);
                return;
            case "default":
                Report("a mod item with AltFunctionUse gets the game's swap icon on Interact", swapDrawn && swapId.EndsWith("ItemSwap1"), seen);
                return;
            case "custom off":
                Report("AltFunctionIcon replaces the icon in the normal mode", swapDrawn && swapTexture == CustomIconWand.Normal.Value.Pointer, seen);
                return;
            case "custom on":
                Report("AltFunctionIcon gets the alternative mode", swapDrawn && swapTexture == CustomIconWand.Alternative.Value.Pointer, seen);
                return;
            case "vanilla near npc":
                vanillaNearNpc = (swapDrawn, swapId);
                Log.Info($"csalticon vanilla near an npc: {seen} interactable={interactable}");
                return;
            case "mod near npc":
                Report("near an npc the mod item draws what the game's item draws", swapDrawn == vanillaNearNpc.Drawn, $"{seen} interactable={interactable} vanilla={vanillaNearNpc}");
                return;
            case "vanilla tap":
                Report("tapping Interact switches the mode of the game's item", Input.ToggleAlternativeFireMode != modeBefore, TapResult(self));
                return;
            case "tap":
                Report("tapping Interact switches the mode of a mod item", Input.ToggleAlternativeFireMode != modeBefore, TapResult(self));
                return;
        }
        if (!s.Tap) return;
        string result = TapResult(self);
        if (s.Type < ItemID.Count)
        {
            vanillaTap[s.Near] = result;
            Log.Info($"csalticon {s.Name}: {result} interactable={interactable}");
            return;
        }
        Report($"{s.Name} does what the game's item does", vanillaTap.TryGetValue(s.Near, out string? vanilla) && vanilla == result, $"mod: {result} interactable={interactable} | game: {vanilla}");
    }

    static void Finish(Player self)
    {
        if (step != 99 && kept != null)
        {
            self.inventory[slot] = kept;
            self.position = home;
            SetMode(false);
            if (!profile.IsNull) VirtualControlsStyles_Layout.Instance.SetActiveProfile(profile);
        }
        step = 99;
        capturing = false;
        Log.Info($"csalticon END pass={pass} fail={fail}");
    }
}
