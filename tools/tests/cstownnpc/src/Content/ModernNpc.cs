using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CsTownNpc.Content;

[AutoloadHead]
public sealed class ModernNpc : TownNpc
{
    public override string HeadTexture => "CsTownNpc/Content/ModernNpc_Head";
    public override void SetChatButtons(ref string button, ref string button2) { button = button2 = ""; }
    public override void RegisterChatButtons(NPCInteractionList interactions) => interactions.Prepend(new ActionButton());
    public override void OnChatButtonClicked(NPCInteraction interaction) => Checks.ModernPost++;
    sealed class ActionButton : NPCInteraction
    {
        public override bool Condition() => true;
        public override string GetText() => "Modern action";
        public override void Interact() { Checks.ModernClicks++; Main.npcChatText = "modern-click"; }
    }
}
