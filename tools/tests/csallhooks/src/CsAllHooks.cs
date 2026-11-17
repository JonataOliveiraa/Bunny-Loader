using System;
using log4net;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsAllHooks;

public sealed class CsAllHooksMod : Mod
{
    const int MarkerFrame = 987654;
    const int MarkerItem = 54321;

    static int frame, pass, fail, portalCalls, bumperCalls, layerCalls, sectionCalls;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsAllHooksMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        On_PortalHelper.UpdatePortalPoints += UpdatePortalPoints;
        On_Minecart.DrawBumper += DrawBumper;
        On_ItemSorting.GetSortingLayerIndex += GetSortingLayerIndex;
        On_WorldSections.SectionLoaded += SectionLoaded;
    }

    static void UpdatePortalPoints(On_PortalHelper.orig_UpdatePortalPoints orig)
    {
        portalCalls++;
        orig();
    }

    static bool DrawBumper(On_Minecart.orig_DrawBumper orig, int frameID)
    {
        bumperCalls++;
        return frameID == MarkerFrame || orig(frameID);
    }

    static int GetSortingLayerIndex(On_ItemSorting.orig_GetSortingLayerIndex orig, int itemType) =>
        itemType == MarkerItem ? 777 : orig(itemType);

    static bool SectionLoaded(On_WorldSections.orig_SectionLoaded orig, WorldSections self, int x, int y)
    {
        sectionCalls++;
        return x == -MarkerItem || orig(self, x, y);
    }

    static void Report(string name, bool ok, string detail = "")
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csallhooks {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (done || Main.gameMenu || i != Main.myPlayer)
            return;
        if (++frame < 180)
            return;
        done = true;
        try
        {
            Checks();
        }
        catch (Exception e)
        {
            Report("exception", false, e.ToString());
        }
        Log.Info($"csallhooks END pass={pass} fail={fail}");
    }

    static void Checks()
    {
        Report("game calls PortalHelper.UpdatePortalPoints", portalCalls > 0, $"calls={portalCalls} frames={frame}");

        bool marker = Minecart.DrawBumper(MarkerFrame);
        int before = bumperCalls;
        bool vanilla = Minecart.DrawBumper(0);
        Report("Minecart.DrawBumper replaced return", marker, $"marker={marker}");
        Report("Minecart.DrawBumper orig runs", bumperCalls == before + 1, $"vanilla(0)={vanilla}");

        int layer = ItemSorting.GetSortingLayerIndex(MarkerItem);
        Report("ItemSorting.GetSortingLayerIndex replaced return", layer == 777, $"got={layer}");
        int sword = ItemSorting.GetSortingLayerIndex(Terraria.ID.ItemID.CopperShortsword);
        Report("ItemSorting.GetSortingLayerIndex orig runs", sword != 777, $"copper={sword}");

        WorldSections sections = Main.sectionManager;
        Report("Main.sectionManager", !sections.IsNull);
        if (sections.IsNull)
            return;
        int calls = sectionCalls;
        bool fake = sections.SectionLoaded(-MarkerItem, 0);
        SectionChecks(sections, calls, fake);
    }

    static void SectionChecks(WorldSections sections, int callsBefore, bool fake)
    {
        Report("WorldSections.SectionLoaded instance hook", fake && sectionCalls == callsBefore + 1, $"fake={fake} calls={sectionCalls - callsBefore}");
        int tx = (int)(Main.LocalPlayer.Center.X / 16) / 200, ty = (int)(Main.LocalPlayer.Center.Y / 16) / 150;
        bool here = sections.SectionLoaded(tx, ty);
        Report("WorldSections.SectionLoaded orig with self", here, $"section=({tx},{ty}) loaded={here} gameCalls={sectionCalls}");
    }
}
