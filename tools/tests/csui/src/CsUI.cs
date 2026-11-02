using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace CsUI;

public sealed class TestState : UIState
{
    public UIPanel Panel = null!;
    public UIText Title = null!;
    public UITextPanel<string> Button = null!;
    public UIImageButton Image = null!;
    public UIList List = null!;
    public UIScrollbar Scrollbar = null!;
    public int ButtonClicks, ImageClicks, ItemClicks, Activated;

    public override void OnActivate() => Activated++;

    public override void OnInitialize()
    {
        Panel = new UIPanel();
        Panel.Left.Set(300f, 0f);
        Panel.Top.Set(100f, 0f);
        Panel.Width.Set(320f, 0f);
        Panel.Height.Set(300f, 0f);
        Append(Panel);

        Title = new UIText("Bunny Loader UI");
        Title.HAlign = 0.5f;
        Panel.Append(Title);

        Button = new UITextPanel<string>("Click me");
        Button.Top.Set(30f, 0f);
        Button.Width.Set(150f, 0f);
        Button.Height.Set(40f, 0f);
        Button.OnLeftClick += (evt, element) => ButtonClicks++;
        Panel.Append(Button);

        Image = new UIImageButton(ModContent.Request<Texture2D>("Terraria/Images/UI/ButtonFavoriteActive"));
        Image.Left.Set(170f, 0f);
        Image.Top.Set(35f, 0f);
        Image.OnLeftClick += (evt, element) => ImageClicks++;
        Panel.Append(Image);

        List = new UIList();
        List.Top.Set(80f, 0f);
        List.Width.Set(260f, 0f);
        List.Height.Set(180f, 0f);
        Panel.Append(List);
        for (int i = 0; i < 20; i++)
        {
            var item = new UITextPanel<string>($"Item {i}");
            item.Width.Set(0f, 1f);
            item.Height.Set(34f, 0f);
            int index = i;
            item.OnLeftClick += (evt, element) => ItemClicks++;
            List.Add(item);
        }

        Scrollbar = new UIScrollbar();
        Scrollbar.Left.Set(265f, 0f);
        Scrollbar.Top.Set(80f, 0f);
        Scrollbar.Height.Set(180f, 0f);
        Panel.Append(Scrollbar);
        List.SetScrollbar(Scrollbar);
    }
}

public sealed class UISystem : ModSystem
{
    public static UserInterface Interface = null!;
    public static TestState State = null!;
    public static List<string> LayerNames = new();
    public static int LayerDraws;
    public static float UIWidth;

    public override void PostSetupContent()
    {
        Interface = new UserInterface();
        State = new TestState();
        State.Activate();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Interface?.CurrentState != null) Interface.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (LayerNames.Count == 0) LayerNames = layers.Select(l => l.Name).ToList();
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index == -1) return;
        layers.Insert(index, new LegacyGameInterfaceLayer("CsUI: Test", delegate
        {
            LayerDraws++;
            UIWidth = Main.screenWidth;
            if (Interface?.CurrentState != null) Interface.Draw(Main.spriteBatch, new GameTime());
            return true;
        }, InterfaceScaleType.UI));
    }
}

public sealed class CsUIMod : Mod
{
    const int End = 2400;
    static int frame, pass, fail;
    static float viewBeforeSwipe;

    static ILog Log => ModContent.GetInstance<CsUIMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Tap(UIElement e)
    {
        Vector2 c = e.GetDimensions().Center();
        Log.Info($"csui TAP {c.X:0.0} {c.Y:0.0}");
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || frame > End) return;
        frame++;
        TestState s = UISystem.State;
        try
        {
            switch (frame)
            {
                case 30:
                    UISystem.Interface.SetState(s);
                    Report("SetState activates the state", s.Activated >= 1 && UISystem.Interface.CurrentState == s, $"activated={s.Activated}");
                    break;
                case 90:
                    Log.Info($"csui layers {string.Join(" | ", UISystem.LayerNames)}");
                    Report("the game has the Vanilla: Mouse Text layer", UISystem.LayerNames.Contains("Vanilla: Mouse Text"), $"{UISystem.LayerNames.Count} layers");
                    Report("the mod layer draws every frame", UISystem.LayerDraws > 30, $"draws={UISystem.LayerDraws}");
                    Report("UIText measures its text", s.Title.MinWidth.Pixels > 20 && s.Title.MinHeight.Pixels > 5, $"{s.Title.MinWidth.Pixels}x{s.Title.MinHeight.Pixels}");
                    Report("the image button takes the texture size", s.Image.GetDimensions().Width > 4, $"{s.Image.GetDimensions().Width}x{s.Image.GetDimensions().Height}");
                    Report("the list lays its items out", s.List.GetTotalHeight() > s.List.GetInnerDimensions().Height && s.List.Count == 20, $"total={s.List.GetTotalHeight()} view={s.List.GetInnerDimensions().Height}");
                    string order = string.Join(",", s.List.Select(e => ((UITextPanel<string>)e).Text.Replace("Item ", "")));
                    Report("the list keeps the order items were added in", order == string.Join(",", Enumerable.Range(0, 20)), order);
                    Report("GetElementAt finds the button", s.GetElementAt(s.Button.GetDimensions().Center()) is UIText or UITextPanel<string>, s.GetElementAt(s.Button.GetDimensions().Center())?.GetType().Name ?? "null");
                    Log.Info($"csui UISCALE {UISystem.UIWidth}");
                    Log.Info("csui SHOT ui");
                    Tap(s.Button);
                    break;
                case 300:
                    Tap(s.Image);
                    break;
                case 500:
                    viewBeforeSwipe = s.List.ViewPosition;
                    CalculatedStyle d = s.List.GetDimensions();
                    Log.Info($"csui SWIPE {d.X + 60:0.0} {d.Y + d.Height - 20:0.0} {d.X + 60:0.0} {d.Y + 20:0.0} 600");
                    break;
                case 800:
                    Report("dragging the list scrolls it and is not a click", s.List.ViewPosition > viewBeforeSwipe + 20 && s.ItemClicks == 0 && s.Scrollbar.ViewPosition == s.List.ViewPosition,
                        $"view {viewBeforeSwipe} -> {s.List.ViewPosition}, item clicks {s.ItemClicks}");
                    Log.Info("csui SHOT scrolled");
                    UIElement first = s.List.First(e => e.GetDimensions().Y >= s.List.GetDimensions().Y);
                    Tap(first);
                    break;
                case 1000:
                    Report("a tap on the text button is one click", s.ButtonClicks == 1, $"clicks={s.ButtonClicks}");
                    Report("a tap on the image button is one click", s.ImageClicks == 1, $"clicks={s.ImageClicks}");
                    Report("a tap on a list item is one click", s.ItemClicks == 1, $"clicks={s.ItemClicks}");
                    UISystem.Interface.SetState(null);
                    Report("SetState(null) hides it", UISystem.Interface.CurrentState == null && !UISystem.Interface.IsVisible, "");
                    break;
            }
        }
        catch (Exception e) { Report($"frame {frame}", false, e.ToString()); }
        if (frame == End) Log.Info($"csui END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"csui {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
