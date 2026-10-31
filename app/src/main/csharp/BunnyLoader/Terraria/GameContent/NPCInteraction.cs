using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace Terraria.GameContent;

public abstract class NPCInteraction
{
    public Player LocalPlayer => Main.LocalPlayer;
    public NPC? TalkNPC => (uint)LocalPlayer.talkNPC < 200u ? Main.npc[LocalPlayer.talkNPC] : null;
    public int TalkNPCType => TalkNPC?.type ?? 0;
    public virtual bool ShowExclamation => false;
    public abstract bool Condition();
    public abstract string GetText();
    public abstract void Interact();
    public virtual bool TryAddCoins(ref Color textColor, out int coins) { coins = 0; return false; }
    public virtual void TextColor(ref Color normal, ref Color mouseOver, bool hovering) { }
}
