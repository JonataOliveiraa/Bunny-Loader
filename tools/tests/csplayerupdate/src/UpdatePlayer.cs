using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CsPlayerUpdate;

public class UpdatePlayer : ModPlayer
{
    public static readonly Dictionary<string, int> Calls = new();
    public static int LastHold = -1, OldLoadout = -1, NewLoadout = -1;

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override void ResetInfoAccessories() => Count(nameof(ResetInfoAccessories));

    public override void NaturalLifeRegen(ref float regen) => Count(nameof(NaturalLifeRegen));

    public override void UpdateAutopause() => Count(nameof(UpdateAutopause));

    public override void ProcessTriggers(TriggersSet triggersSet) => Count(nameof(ProcessTriggers));

    public override void ArmorSetBonusActivated() => Count(nameof(ArmorSetBonusActivated));

    public override void ArmorSetBonusHeld(int holdTime)
    {
        Count(nameof(ArmorSetBonusHeld));
        LastHold = holdTime;
    }

    public override void UpdateVisibleAccessories() => Count(nameof(UpdateVisibleAccessories));

    public override void UpdateVisibleVanityAccessories() => Count(nameof(UpdateVisibleVanityAccessories));

    public override void UpdateDyes() => Count(nameof(UpdateDyes));

    public override void PostUpdateMiscEffects() => Count(nameof(PostUpdateMiscEffects));

    public override void PostUpdateRunSpeeds() => Count(nameof(PostUpdateRunSpeeds));

    public override void PreUpdateMovement() => Count(nameof(PreUpdateMovement));

    public override void OnEquipmentLoadoutSwitched(int oldLoadoutIndex, int loadoutIndex)
    {
        Count(nameof(OnEquipmentLoadoutSwitched));
        OldLoadout = oldLoadoutIndex;
        NewLoadout = loadoutIndex;
    }
}
