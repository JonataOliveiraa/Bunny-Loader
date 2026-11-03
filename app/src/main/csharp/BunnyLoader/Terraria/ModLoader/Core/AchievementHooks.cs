using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.Localization;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static unsafe class AchievementHooks
{
    const int Frame = 66;
    const int SheetColumns = 8;

    static readonly HashSet<IntPtr> refreshedMenus = new();
    static LocalizedText hiddenText;
    static bool remapped;
    static int remappedX, remappedY;

    internal static void Install() => ItemHooks.Once("ModAchievement", () =>
    {
        On_Achievement.OnConditionComplete += OnConditionComplete;
        On_GUIAchievementsMenu.Open += Open;
        On_GUIAchievementsMenu.AchievementDraw += AchievementDraw;
        On_InGamePopups_AchievementUnlockedPopup.DrawInGame += (orig, self, sb, anchor) =>
        {
            bool remap = UsePopupTexture(self);
            try { orig(self, sb, anchor); }
            finally { if (remap) Host.Native()->EndDrawCapture(); }
        };
        On_InGamePopups_AchievementUnlockedPopup.DrawInNotificationsArea += DrawInNotificationsArea;
    });

    internal static void InstallHelper() => ItemHooks.Once("AchievementsHelper", () =>
    {
        On_AchievementsHelper.NotifyItemPickup_Player_Item += (orig, player, item) =>
        {
            orig(player, item);
            Fire("OnItemPickup", () => AchievementsHelper.itemPickup?.Invoke(player, (short)item.type, item.stack));
        };
        On_AchievementsHelper.NotifyItemPickup_Player_Item_int += (orig, player, item, customStack) =>
        {
            orig(player, item, customStack);
            Fire("OnItemPickup", () => AchievementsHelper.itemPickup?.Invoke(player, (short)item.type, customStack));
        };
        On_AchievementsHelper.NotifyItemCraft += (orig, recipe) =>
        {
            orig(recipe);
            Fire("OnItemCraft", () => AchievementsHelper.itemCraft?.Invoke((short)recipe.createItem.type, recipe.createItem.stack));
        };
        On_AchievementsHelper.NotifyTileDestroyed += (orig, player, tile) =>
        {
            orig(player, tile);
            if (!Main.gameMenu && AchievementsHelper._isMining)
                Fire("OnTileDestroyed", () => AchievementsHelper.tileDestroyed?.Invoke(player, tile));
        };
        On_AchievementsHelper.NotifyNPCKilledDirect += (orig, player, npcNetID) =>
        {
            orig(player, npcNetID);
            Fire("OnNPCKilled", () => AchievementsHelper.npcKilled?.Invoke(player, (short)npcNetID));
        };
        On_AchievementsHelper.NotifyProgressionEvent += (orig, eventID) =>
        {
            orig(eventID);
            if (Main.netMode != 2)
                Fire("OnProgressionEvent", () => AchievementsHelper.progressionEvent?.Invoke(eventID));
        };
    });

    static void Fire(string name, Action action)
    {
        try { action(); }
        catch (Exception e) { ItemHooks.Report($"AchievementsHelper.{name}", e); }
    }

    static void OnConditionComplete(On_Achievement.orig_OnConditionComplete orig, Achievement self, AchievementCondition condition)
    {
        ModAchievement? achievement = AchievementLoader.Of(self);
        bool before = achievement == null || self.IsCompleted;
        orig(self, condition);
        if (before || !self.IsCompleted)
            return;
        try { achievement!.OnCompleted(self); }
        catch (Exception e) { ItemHooks.Report($"{achievement!.Name}.OnCompleted", e); }
    }

    static void Open(On_GUIAchievementsMenu.orig_Open orig, GUIAchievementsMenu self, int backTo, int selected)
    {
        if (refreshedMenus.Add(self.Pointer))
            self._achievements = default;
        orig(self, backTo, selected);
    }

    static void AchievementDraw(On_GUIAchievementsMenu.orig_AchievementDraw orig, GUIAchievementsMenu self, ItemGrid_Layout gridLayout, int index, Vector2 position, float scale)
    {
        var list = new GameList(self.SortedAchievementsData);
        Achievement achievement = !list.IsNull && index >= 0 && index < list.Count ? list.Get<Achievement>(index) : default;
        ModAchievement? modAchievement = AchievementLoader.Of(achievement);
        Texture2D texture = modAchievement != null ? TextureOf(modAchievement) : default;
        if (texture.IsNull)
        {
            orig(self, gridLayout, index, position, scale);
            return;
        }

        Texture2D icons = GUIAchievementsMenu.AchievementsIcons;
        LocalizedText name = achievement.FriendlyName, description = achievement.Description;
        bool hide = false;
        if (!achievement.IsCompleted)
        {
            try { hide = modAchievement!.Hidden; }
            catch (Exception e) { ItemHooks.Report($"{modAchievement!.Name}.Hidden", e); }
        }
        if (hide)
        {
            if (hiddenText.IsNull)
                hiddenText = Language.GetText("???");
            achievement.FriendlyName = hiddenText;
            achievement.Description = hiddenText;
        }
        GUIAchievementsMenu.AchievementsIcons = texture;
        bool remap = BeginRemap(texture);
        try
        {
            orig(self, gridLayout, index, position, scale);
        }
        finally
        {
            if (remap) Host.Native()->EndDrawCapture();
            GUIAchievementsMenu.AchievementsIcons = icons;
            if (hide)
            {
                achievement.FriendlyName = name;
                achievement.Description = description;
            }
        }
    }

    static void DrawInNotificationsArea(On_InGamePopups_AchievementUnlockedPopup.orig_DrawInNotificationsArea orig, InGamePopups.AchievementUnlockedPopup self,
        SpriteBatch spriteBatch, Rectangle area, ref int gamepadPointLocalIndexTouse)
    {
        bool remap = UsePopupTexture(self);
        try { orig(self, spriteBatch, area, ref gamepadPointLocalIndexTouse); }
        finally { if (remap) Host.Native()->EndDrawCapture(); }
    }

    static bool UsePopupTexture(InGamePopups.AchievementUnlockedPopup self)
    {
        ModAchievement? achievement = AchievementLoader.Of(self._theAchievement);
        Texture2D texture = achievement != null ? TextureOf(achievement) : default;
        if (texture.IsNull)
            return false;
        if (self._achievementTexture?.Pointer != achievement!.Texture.GameAsset)
            self._achievementTexture = achievement.Texture;
        return BeginRemap(texture);
    }

    static Texture2D TextureOf(ModAchievement achievement)
    {
        try { return achievement.Texture.Value; }
        catch (Exception e)
        {
            ItemHooks.Report($"{achievement.Name}.Texture", e);
            return default;
        }
    }

    static bool BeginRemap(Texture2D texture)
    {
        remapped = false;
        return texture.Width <= 2 * Frame && Host.Native()->BeginDrawCapture(texture.Pointer, &Remap) != 0;
    }

    [UnmanagedCallersOnly]
    static int Remap(DrawCapture* draw)
    {
        if (draw->HasSource == 0)
            return 0;
        if (remapped && draw->SourceX == remappedX && draw->SourceY == remappedY)
            return 0;
        int column = draw->SourceX / Frame, row = draw->SourceY / Frame;
        int icon = row * SheetColumns + column % SheetColumns;
        draw->SourceX = (column >= SheetColumns ? Frame : 0) + draw->SourceX % Frame;
        draw->SourceY = icon * Frame + draw->SourceY % Frame;
        remapped = true;
        remappedX = draw->SourceX;
        remappedY = draw->SourceY;
        return 0;
    }
}
