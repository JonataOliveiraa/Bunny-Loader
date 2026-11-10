using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace Terraria.ModLoader.Core;

internal static class SubworldVoteUI
{
    const string LayerName = "BunnyLoader: Subworld Vote";

    static UserInterface? ui;
    static VoteState? state;

    sealed class Portraits : UIElement
    {
        internal const float Size = 60f, Gap = 14f;
        const int Frame = 2;
        const float HeadScale = 1.2f;
        static readonly Color Background = new(12, 12, 20);

        internal readonly List<int> Players = new();
        internal readonly List<Color> Borders = new();

        internal static float RowWidth(int count) => count * Size + Math.Max(0, count - 1) * Gap;

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle d = GetDimensions();
            float left = d.X + d.Width / 2f - RowWidth(Players.Count) / 2f;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < Players.Count; i++)
            {
                var box = new Rectangle((int)(left + i * (Size + Gap)), (int)d.Y, (int)Size, (int)Size);
                spriteBatch.Draw(pixel, box, Borders[i]);
                box.Inflate(-Frame, -Frame);
                spriteBatch.Draw(pixel, box, Background);
            }

            for (int i = 0; i < Players.Count; i++)
            {
                Player player = Main.player[Players[i]];
                if (player.IsNull || !player.active) continue;
                var center = new Vector2(left + i * (Size + Gap) + Size / 2f, d.Y + Size / 2f);
                Main.PlayerRenderer.DrawPlayerHead(Main.Camera, player, center, 1f, HeadScale, Color.White);
            }
        }
    }

    sealed class Label : UIElement
    {
        internal string Text;
        readonly float scale;

        internal Label(string text, float scale)
        {
            Text = text;
            this.scale = scale;
            Width.Set(0f, 1f);
            Height.Set(28f * scale, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle d = GetDimensions();
            Utils.DrawBorderString(spriteBatch, Text, new Vector2(d.X + d.Width / 2f, d.Y + d.Height / 2f), Color.White, scale, 0.5f, 0.4f);
        }
    }

    sealed class Button : UIPanel
    {
        internal bool Visible = true;

        internal Button(string key, float align, Color color)
        {
            Width.Set(170f, 0f);
            Height.Set(44f, 0f);
            HAlign = align;
            VAlign = 1f;
            BackgroundColor = color;
            var label = new Label(Language.GetTextValue(key), 1f);
            label.VAlign = 0.5f;
            Append(label);
        }

        public override bool ContainsPoint(Vector2 point) => Visible && base.ContainsPoint(point);

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (Visible) base.Draw(spriteBatch);
        }
    }

    sealed class VoteState : UIState
    {
        UIPanel panel = null!;
        UIText title = null!;
        Label prompt = null!, countdown = null!;
        UIElement names = null!;
        Portraits row = null!;
        Button accept = null!, decline = null!;
        string shownTitle = "";

        public override void OnInitialize()
        {
            panel = new UIPanel();
            panel.Width.Set(480f, 0f);
            panel.Height.Set(270f, 0f);
            panel.HAlign = 0.5f;
            panel.VAlign = 0.4f;
            Append(panel);

            prompt = new Label(Language.GetTextValue("BunnyLoader.SubworldRequest"), 0.9f);
            panel.Append(prompt);

            title = new UIText("", 0.6f, true);
            title.HAlign = 0.5f;
            title.Top.Set(24f, 0f);
            panel.Append(title);

            countdown = new Label("", 0.9f);
            countdown.Top.Set(166f, 0f);
            panel.Append(countdown);

            accept = new Button("BunnyLoader.SubworldAccept", 0.1f, new Color(40, 120, 60));
            accept.OnLeftClick += (_, _) => SubworldNet.Answer(true);
            panel.Append(accept);
            decline = new Button("BunnyLoader.SubworldDecline", 0.9f, new Color(140, 45, 45));
            decline.OnLeftClick += (_, _) => SubworldNet.Answer(false);
            panel.Append(decline);

            names = Row(new UIElement());
            row = Row(new Portraits());
        }

        T Row<T>(T element) where T : UIElement
        {
            element.Width.Set(0f, 1f);
            element.Height.Set(86f, 0f);
            element.Top.Set(76f, 0f);
            panel.Append(element);
            return element;
        }

        internal void Refresh(SubworldNet.Vote vote)
        {
            if (vote.Name != shownTitle)
            {
                shownTitle = vote.Name;
                title.SetText(vote.Name);
                Recalculate();
            }

            countdown.Text = vote.Closing == null ? Language.GetTextValue("BunnyLoader.SubworldSeconds", vote.SecondsLeft) : "";
            bool answering = !SubworldNet.IsHost && vote.Closing == null && vote.AnswerOf(Main.myPlayer) == SubworldNet.Pending;
            accept.Visible = answering;
            decline.Visible = answering;

            if (!Same(vote.Players))
            {
                names.RemoveAllChildren();
                row.Players.Clear();
                row.Players.AddRange(vote.Players);
                float width = Portraits.RowWidth(vote.Players.Count);
                for (int i = 0; i < vote.Players.Count; i++)
                {
                    if (vote.Players[i] != Main.myPlayer) continue;
                    var you = new Label(Language.GetTextValue("BunnyLoader.SubworldYou"), 0.85f);
                    you.Left.Set(-width / 2f + i * (Portraits.Size + Portraits.Gap), 0.5f);
                    you.Width.Set(Portraits.Size, 0f);
                    you.Top.Set(Portraits.Size + 2f, 0f);
                    names.Append(you);
                }

                Recalculate();
            }

            row.Borders.Clear();
            foreach (int player in row.Players)
            {
                byte answer = vote.AnswerOf(player);
                row.Borders.Add(answer == SubworldNet.Yes ? new Color(60, 220, 80)
                    : answer == SubworldNet.No ? new Color(230, 50, 50)
                    : player == Main.myPlayer ? new Color(255, 215, 0)
                    : new Color(120, 120, 140));
            }
        }

        bool Same(List<int> players)
        {
            if (players.Count != row.Players.Count) return false;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != row.Players[i]) return false;
            return true;
        }
    }

    internal static void Show()
    {
        try
        {
            if (ui == null)
            {
                ui = new UserInterface();
                state = new VoteState();
                state.Activate();
            }

            if (ui.CurrentState != state) ui.SetState(state);
            InterfaceLayerHooks.Install();
        }
        catch (Exception e) { ItemHooks.Report("Subworld vote window", e); }
    }

    internal static void Update(GameTime gameTime)
    {
        if (ui?.CurrentState == null || state == null) return;
        SubworldNet.Vote? vote = SubworldNet.Current;
        if (vote == null || Main.gameMenu)
        {
            ui.SetState(null);
            return;
        }

        try
        {
            state.Refresh(vote);
            ui.Update(gameTime);
        }
        catch (Exception e) { ItemHooks.Report("Subworld vote window", e); }
    }

    internal static void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (ui?.CurrentState == null) return;
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        var layer = new LegacyGameInterfaceLayer(LayerName, () =>
        {
            ui?.Draw(Main.spriteBatch, new GameTime());
            return true;
        }, InterfaceScaleType.UI);
        if (index < 0) layers.Add(layer);
        else layers.Insert(index, layer);
    }
}
