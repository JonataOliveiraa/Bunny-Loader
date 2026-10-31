using System;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria.ModLoader.Core;

internal static unsafe class TownAttackHooks
{
    struct AttackArgs
    {
        public int Damage;
        public float Knockback;
        public int Cooldown, Extra, Projectile, Delay;
        public float Speed, Gravity, Random, Aura;
        public int Width, Height, InBetween;
    }
    struct DrawArgs
    {
        public IntPtr Texture;
        public Rectangle Frame;
        public float Scale;
        public int Size;
        public Vector2 Offset;
    }

    internal static void Install(int type, NPCMethod flags, bool chat)
    {
        uint wanted = ((flags & NPCMethod.TownAttack) != 0 ? 1u : 0u) | ((flags & NPCMethod.TownAttackDraw) != 0 ? 2u : 0u) | (chat ? 4u : 0u);
        if (wanted != 0 && Host.Native()->InstallTownNPCHooks(type, wanted, &Event) == 0)
            throw new InvalidOperationException($"Town NPC hooks could not be installed for type {type}");
    }

    internal static void InstallAll()
    {
        if (Host.Native()->InstallTownNPCHooks(-1, 1, &Event) == 0) throw new InvalidOperationException("Town NPC attack hooks could not be installed for every NPC");
    }

    [UnmanagedCallersOnly]
    static void Event(int kind, IntPtr pointer, void* arguments)
    {
        NPC npc = new(pointer);
        try
        {
            if (kind == 6)
            {
                bool? value = NPCLoader.CanChat(npc);
                if (value.HasValue) *(int*)arguments = value.Value ? 1 : 0;
                return;
            }
            if (kind >= 4)
            {
                ref DrawArgs draw = ref *(DrawArgs*)arguments;
                Texture2D item = new(draw.Texture);
                if (kind == 4) NPCLoader.DrawTownAttackGun(npc, ref item, ref draw.Frame, ref draw.Scale, ref draw.Size);
                else NPCLoader.DrawTownAttackSwing(npc, ref item, ref draw.Frame, ref draw.Size, ref draw.Scale, ref draw.Offset);
                draw.Texture = item.Pointer;
                return;
            }
            ref AttackArgs args = ref *(AttackArgs*)arguments;
            args.Damage = (int)(args.Damage * NPCExtraHooks.TownDamage(npc));
            NPCLoader.TownNPCAttackStrength(npc, ref args.Damage, ref args.Knockback);
            NPCLoader.TownNPCAttackCooldown(npc, ref args.Cooldown, ref args.Extra);
            if (kind == 3) NPCLoader.TownNPCAttackSwing(npc, ref args.Width, ref args.Height);
            else
            {
                NPCLoader.TownNPCAttackProj(npc, ref args.Projectile, ref args.Delay);
                NPCLoader.TownNPCAttackProjSpeed(npc, ref args.Speed, ref args.Gravity, ref args.Random);
                if (kind == 1)
                {
                    bool inBetween = args.InBetween != 0;
                    NPCLoader.TownNPCAttackShoot(npc, ref inBetween);
                    args.InBetween = inBetween ? 1 : 0;
                }
                if (kind == 2) NPCLoader.TownNPCAttackMagic(npc, ref args.Aura);
            }
        }
        catch (Exception e) { ItemHooks.Report("Town NPC hooks", e); }
    }
}
