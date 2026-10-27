using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BunnyLoader.Loaders;

// O tooltip de arma de uma classe de gancho (Generic, Throwing,
// MagicSummonHybrid e as de mod). O do celular
// (Main.MouseText_DrawItemTooltip_GetLinesInfo) nao chama o GetWeaponDamage:
// monta o dano pelos campos de classe e escreve o nome pelas flags ("dano
// corpo a corpo"), e o critico so para melee/ranged/magic. Depois dele, as
// linhas de dano e critico sao refeitas pela classe, e o ShowStatTooltipLine
// esconde as que a classe nao quer. Os itens das classes do jogo nao mudam.
internal static class DamageClassTooltips
{
    sealed class Line
    {
        public string Text = "";
        public bool Prefix, Bad;
        public int Origin = -1; // o indice no jogo; -1 = linha nova
    }

    public static void Install()
    {
        On_Main.MouseText_DrawItemTooltip_GetLinesInfo += (orig, item, ref yoyoLogo, ref researchLine, ref materialsLine, oldKB,
            ref numLines, toolTipLine, preFixLine, badPreFixLine, ref setBonusLine, ref setBonusColour, ref sharedLine) =>
        {
            orig(item, ref yoyoLogo, ref researchLine, ref materialsLine, oldKB, ref numLines, toolTipLine, preFixLine,
                badPreFixLine, ref setBonusLine, ref setBonusColour, ref sharedLine);
            try
            {
                Rewrite(item, ref numLines, toolTipLine, preFixLine, badPreFixLine,
                    ref yoyoLogo, ref researchLine, ref materialsLine, ref setBonusLine, ref sharedLine);
            }
            catch (Exception e)
            {
                Log.Error($"damage class tooltip: {e}");
            }
        };
    }

    static string Tip(int index) => Lang.tip[index].Value ?? "";

    static bool StartsWithNumber(string text) => text.Length > 0 && (char.IsDigit(text[0]) || (text[0] == '-' && text.Length > 1 && char.IsDigit(text[1])));

    static void Rewrite(Item item, ref int numLines, GameArray<GameString> text, GameArray<bool> prefix, GameArray<bool> bad,
        ref int yoyoLogo, ref int researchLine, ref int materialsLine, ref int setBonusLine, ref int sharedLine)
    {
        if (item.Pointer == 0 || item.damage <= 0) return;
        DamageClass cls = DamageClassLoader.ItemClass(item);
        if (!DamageClassLoader.IsHooked(cls)) return;
        Player me = Main.LocalPlayer;
        if (me.Pointer == 0) return;

        var lines = new List<Line>(numLines + 2);
        for (int i = 0; i < numLines; i++)
            lines.Add(new Line { Text = text[i].ToString() ?? "", Prefix = prefix[i], Bad = bad[i], Origin = i });

        // Erro do mod: a linha fica, como no loader JS.
        bool Show(string line) => DamageClassLoader.Safe(cls, nameof(DamageClass.ShowStatTooltipLine), () => cls.ShowStatTooltipLine(me, line), true);

        // A linha de dano do jogo: o numero mais o nome pelas flags.
        string suffix = item.melee ? Tip(2) : item.ranged ? Tip(3) : item.magic ? Tip(4) : item.summon ? Tip(53) : Tip(55);
        int damageAt = lines.FindIndex(l => l.Origin >= 0 && StartsWithNumber(l.Text) && l.Text.EndsWith(suffix, StringComparison.Ordinal));
        if (damageAt < 0) return; // nao e uma arma no tooltip (municao sem uso, por exemplo)

        if (Show("Damage"))
        {
            int damage = (int)(item.damage * ItemID.Sets.ToolTipDamageMultiplier[item.type]);
            if (ItemID.Sets.RapidAttackBonusDamage[item.type]) damage = me.ApplyRapidAttackBonus(damage, item.type, false);
            int value = (int)(me.GetWeaponDamageMultiplier(item) * damage + 5e-6f);
            string name = DamageClassLoader.NameOf(cls);
            lines[damageAt].Text = value + (cls.IsVanilla ? "" : " ") + name;
        }

        // O critico: o do jogo (pelas flags) logo depois do dano, ou nenhum.
        string critSuffix = Tip(5);
        int critAt = damageAt + 1 < lines.Count && StartsWithNumber(lines[damageAt + 1].Text) &&
                     lines[damageAt + 1].Text.EndsWith(critSuffix, StringComparison.Ordinal) ? damageAt + 1 : -1;
        int? crit = null;
        if (DamageClassLoader.UsesCrit(cls) && Show("CritChance"))
        {
            int c = DamageClassLoader.RoundHalfUp(me.GetTotalCritChance(cls)) + item.GetVisualCritChance(me);
            if (c > 0) crit = c;
        }
        if (crit is int shown)
        {
            if (critAt >= 0) lines[critAt].Text = shown + critSuffix;
            else lines.Insert(damageAt + 1, new Line { Text = shown + critSuffix });
        }
        else if (critAt >= 0)
        {
            lines.RemoveAt(critAt);
        }

        // Velocidade e repulsao: o texto fixo do jogo (Lang.tip 6-13 e 14-22).
        int speedAt = FindTip(lines, 6, 13);
        if (speedAt >= 0 && !Show("Speed")) lines.RemoveAt(speedAt);

        int kbAt = FindTip(lines, 14, 22);
        if (kbAt >= 0)
        {
            if (!Show("Knockback"))
            {
                lines.RemoveAt(kbAt);
            }
            else if (DamageClassLoader.KnockbackIfAny(me, cls) is StatModifier km && km != StatModifier.Default)
            {
                lines[kbAt].Text = Tip(KnockbackTip(Knockback(item, me, km)));
            }
        }

        if (lines.Count > text.Length) lines.RemoveRange(text.Length, lines.Count - text.Length);
        var moved = new Dictionary<int, int>();
        for (int i = 0; i < lines.Count; i++)
        {
            Line l = lines[i];
            if (l.Origin >= 0) moved[l.Origin] = i;
            text[i] = GameString.From(l.Text);
            prefix[i] = l.Prefix;
            bad[i] = l.Bad;
        }
        numLines = lines.Count;
        Move(ref yoyoLogo, moved);
        Move(ref researchLine, moved);
        Move(ref materialsLine, moved);
        Move(ref setBonusLine, moved);
        Move(ref sharedLine, moved);
    }

    // Os marcadores do jogo (onde vai o logo do ioio, a linha de pesquisa...)
    // seguem a linha deles; a que sumiu vira -1.
    static void Move(ref int marker, Dictionary<int, int> moved)
    {
        if (marker < 0) return;
        marker = moved.TryGetValue(marker, out int now) ? now : -1;
    }

    static int FindTip(List<Line> lines, int first, int last)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Origin < 0) continue;
            for (int t = first; t <= last; t++)
                if (lines[i].Text == Tip(t)) return i;
        }
        return -1;
    }

    // A repulsao como o tooltip do jogo a calcula, com a da classe por cima.
    static float Knockback(Item item, Player me, StatModifier classKnockback)
    {
        const int ArrowAmmo = 40;   // AmmoID.Arrow
        const int StakeAmmo = 1835; // AmmoID.Stake
        float kb = item.knockBack;
        if (item.summon) kb += me.minionKB;
        if ((me.magicQuiver && item.useAmmo == ArrowAmmo) || item.useAmmo == StakeAmmo) kb = (int)(kb * 1.1f);
        if (me.inventory[me.selectedItem].type == 3106 && item.type == 3106) kb += kb * (1f - me.stealth);
        return classKnockback.ApplyTo(kb);
    }

    static int KnockbackTip(float kb) =>
        kb == 0f ? 14 : kb <= 1.5f ? 15 : kb <= 3f ? 16 : kb <= 4f ? 17 : kb <= 6f ? 18 : kb <= 7f ? 19 : kb <= 9f ? 20 : kb <= 11f ? 21 : 22;
}
