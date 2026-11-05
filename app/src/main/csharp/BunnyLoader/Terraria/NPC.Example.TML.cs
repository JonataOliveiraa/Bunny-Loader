using Microsoft.Xna.Framework;
using BunnyLoader.Interop;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct NPC
{
    public int GetPartyHatGore() => GetPartyHatColor() switch
    {
        PartyHatColor.Cyan => 940,
        PartyHatColor.Pink => 939,
        PartyHatColor.Purple => 941,
        PartyHatColor.White => 942,
        PartyHatColor.None => 0,
        _ => 926,
    };

    public static NPC NewNPCDirect(IEntitySource source, int x, int y, int type, int start = 0, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f, float ai3 = 0f, int target = 255)
    {
        int index = NewNPC(source, x, y, type, start, ai0, ai1, ai2, ai3, target);
        return Main.npc[index];
    }

    public static NPC NewNPCDirect(IEntitySource source, Vector2 position, int type, int start = 0, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f, float ai3 = 0f, int target = 255) =>
        NewNPCDirect(source, (int)position.X, (int)position.Y, type, start, ai0, ai1, ai2, ai3, target);

    public int SimpleStrikeNPC(int damage, int hitDirection, bool crit = false, float knockBack = 0f, DamageClass? damageType = null, bool damageVariation = false, float luck = 0f, bool noPlayerInteraction = false)
    {
        if (damageVariation) damage = Main.DamageVar(damage, luck);
        return noPlayerInteraction ? StrikeNPCNoInteraction(damage, knockBack, hitDirection) : StrikeNPC(damage, knockBack, hitDirection, crit);
    }
}
