// As linhas do tooltip de um item, como o Main.MouseText_DrawItemTooltip_GetLinesInfo
// do celular as monta, mas já como TooltipLine com os nomes do tModLoader
// ('ItemName', 'Damage', 'CritChance', 'Tooltip0', 'PrefixDamage'...), para o
// ModifyTooltips achar a linha pelo nome. Porte do GST378.
//
// Só roda quando o TooltipLoader desenha o tooltip de um item que pede (mod
// com ModifyTooltips ou os Draw*Tooltip*); os outros usos do método são do jogo.
// Cada linha leva o papel dela em __role (a cor sai dele depois do ModifyTooltips):
// 'name', 'setBonus', 'research', 'shared', 'materials', 'price'.
class VanillaTooltips {
    static #RoundEven(x) {
        const floor = Math.floor(x);
        if (x - floor === 0.5) return floor % 2 === 0 ? floor : floor + 1;
        return Math.round(x);
    }

    static TextValue(key, ...args) {
        const text = Terraria.Localization.Language['string GetTextValue(string key)'](key);
        return args.reduce((t, arg, i) => t.replaceAll('{' + i + '}', arg), text);
    }

    // { lines, setBonusColor, prefixEnd }: prefixEnd é onde entram as linhas
    // do prefixo de mod (depois das do jogo), ou -1 sem prefixo.
    static Build(item, oldKB) {
        const { Lang, Main, NPC } = Terraria;
        const { AmmoID, BuffID, ItemID, ProjectileID, TileID } = Terraria.ID;
        const text = VanillaTooltips.TextValue;
        const lines = [];
        const out = { lines, setBonusColor: Color.White, prefixEnd: -1 };
        const add = (name, value, role) => {
            const line = new TooltipLine('Terraria', name, value);
            if (role) line.__role = role;
            lines.push(line);
            return line;
        };
        const me = Main.LocalPlayer;

        add('ItemName', item.HoverName, 'name');

        // Favorito, e as casas compartilhadas entre os equipamentos (loadouts).
        let isLoadoutSlot = false;
        let showShareHint = false;
        const ItemSlot = Terraria.UI.ItemSlot;
        if (item.tooltipContext >= 0 && ItemSlot.canLoadoutShareAt[item.tooltipContext]) {
            const from = new Ref(null);
            ItemSlot.GetSharedLoadoutItem(item.tooltipContext, item.tooltipSlot, from);
            if (from.value !== null && from.value !== undefined) add('SharedFrom', text('UI.ItemLoadoutSharedFrom', from.value + 1));
            else if (item.favorited) add('Shared', text('UI.ItemLoadoutShared'));
            else showShareHint = true;
            isLoadoutSlot = true;
        } else if (item.favorited) {
            add('Favorite', Lang.tip[56].Value);
            add('FavoriteDesc', Lang.tip[57].Value);
            if (me.chest !== -1) {
                const container = me.GetCurrentContainer().item;
                if (Terraria.UI.ChestUI.IsBlockedFromTransferIntoChest(item, container)) {
                    add('NoTransfer', text('UI.ItemCannotBePlacedInsideItself'));
                }
            }
        }

        if (item.social && !item.vanity && !item.hasVanityEffects) add('NoSocial', Lang.tip[61].Value);

        // Armas
        if (item.damage > 0 && (!item.notAmmo || item.useStyle !== 0) &&
            (item.type < 71 || item.type > 74 || me['bool HasItem(int type)'](905))) {
            let damage = (item.damage * ItemID.Sets.ToolTipDamageMultiplier[item.type]) | 0;
            if (ItemID.Sets.RapidAttackBonusDamage[item.type]) damage = me.ApplyRapidAttackBonus(damage, item.type, false);
            let value = Math.trunc(me.GetWeaponDamageMultiplier(item) * damage + 5e-6).toString();
            if (item.melee) value += Lang.tip[2].Value;
            else if (item.ranged) value += Lang.tip[3].Value;
            else if (item.magic) value += Lang.tip[4].Value;
            else if (item.summon) value += Lang.tip[53].Value;
            else value += Lang.tip[55].Value;
            add('Damage', value);

            const selectedCrit = me.inventory[me.selectedItem].crit;
            let crit = null;
            if (item.melee) crit = me.meleeCrit - selectedCrit + item.GetVisualCritChance(me);
            else if (item.ranged) crit = me.rangedCrit - selectedCrit + item.GetVisualCritChance(me);
            else if (item.magic) crit = me.magicCrit - selectedCrit + item.GetVisualCritChance(me);
            if (crit !== null) add('CritChance', crit + Lang.tip[5].Value);

            if (item.useStyle !== 0 && (!item.summon || (item.shoot >= 0 && ProjectileID.Sets.IsAWhip[item.shoot]))) {
                const a = item.useAnimation;
                const tip = a <= 8 ? 6 : a <= 20 ? 7 : a <= 25 ? 8 : a <= 30 ? 9 : a <= 35 ? 10 : a <= 45 ? 11 : a <= 55 ? 12 : 13;
                add('Speed', Lang.tip[tip].Value);
            }

            let kb = item.knockBack;
            if (item.summon) kb += me.minionKB;
            if ((me.magicQuiver && item.useAmmo === AmmoID.Arrow) || item.useAmmo === AmmoID.Stake) kb = (kb * 1.1) | 0;
            if (me.inventory[me.selectedItem].type === 3106 && item.type === 3106) kb += kb * (1 - me.stealth);
            const tip = kb === 0 ? 14 : kb <= 1.5 ? 15 : kb <= 3 ? 16 : kb <= 4 ? 17 : kb <= 6 ? 18 : kb <= 7 ? 19 : kb <= 9 ? 20 : kb <= 11 ? 21 : 22;
            add('Knockback', Lang.tip[tip].Value);
        }

        // Pesca e isca
        if (item.fishingPole > 0) {
            add('FishingPower', text('GameUI.PrecentFishingPower', item.fishingPole));
            add('NeedsBait', text('GameUI.BaitRequired'));
        }
        if (item.bait > 0) add('BaitPower', text('GameUI.BaitPower', item.bait));

        if (!isLoadoutSlot && (item.headSlot > 0 || item.bodySlot > 0 || item.legSlot > 0 || item.accessory ||
            Main.projHook[item.shoot] || item.mountType !== -1 ||
            (item.buffType > 0 && (Main.lightPet[item.buffType] || Main.vanityPet[item.buffType])))) {
            add('Equipable', (item.type === 854 || item.type === 3035) && Main.npcShop > 0 ? Lang.tip[60].Value : Lang.tip[23].Value);
        }

        if (item.tileWand > 0) add('WandConsumes', Lang.tip[52].Value + Lang.GetItemNameValue(item.tileWand));
        if (item.questItem) add('Quest', Lang.inter[65].Value);
        if (item.vanity) add('Vanity', Lang.tip[24].Value);
        if (item.defense > 0) add('Defense', item.defense + Lang.tip[25].Value);
        if (item.pick > 0) add('PickPower', item.pick + Lang.tip[26].Value);
        if (item.axe > 0) add('AxePower', item.axe * 5 + Lang.tip[27].Value);
        if (item.hammer > 0) add('HammerPower', item.hammer + Lang.tip[28].Value);
        if (item.tileBoost !== 0) add('TileBoost', (item.tileBoost > 0 ? '+' : '') + item.tileBoost + Lang.tip[54].Value);

        if (item.healLife > 0) {
            add('HealLife', item.type === 3001 ? text('CommonItemTooltip.RestoresLifeRange', item.healLife, 120)
                                               : text('CommonItemTooltip.RestoresLife', item.healLife));
        }
        if (item.healMana > 0) add('HealMana', text('CommonItemTooltip.RestoresMana', item.healMana));

        if (item.mana > 0 && ((item.type !== 127 && item.type !== 4347 && item.type !== 4348 && item.type !== 514) || !me.spaceGun)) {
            add('UseMana', text('CommonItemTooltip.UsesMana', (item.mana * me.manaCost) | 0));
        }

        if (item.createWall > 0 || item.createTile > -1 || item.type === 849) {
            if (ItemID.Sets.PlaceTileOnAltUse[item.type] || item.consumable) add('Placeable', Lang.tip[33].Value);
        } else if (item.ammo > 0 && !item.notAmmo) {
            add('Ammo', Lang.tip[34].Value);
        } else if (item.consumable && !item.chlorophyteExtractinatorConsumable) {
            add('Consumable', Lang.tip[35].Value);
        }

        if (item.material) add('Material', Lang.tip[36].Value);

        const wiring = item.createTile > -1 && (TileID.Sets.Wiring.IsATrigger[item.createTile] || TileID.Sets.Wiring.IsAMechanism[item.createTile]);
        if (wiring && (item.createTile !== 105 || ItemID.Sets.IsWireableStatue[item.type])) add('Wireable', text('CommonItemTooltip.Wireable'));
        if (item.createTile === 21 || item.createTile === 467) add('Container', text('CommonItemTooltip.Container'));
        if (item.createTile === 441 || item.createTile === 468) {
            add('WireTrigger', Lang.ToopltipProcessor(item.type, text('CommonItemTooltip.WireTrigger')));
        }

        const tooltip = item.ToolTip;
        if (tooltip !== null) {
            for (let i = 0; i < tooltip.Lines; i++) {
                const cursed = i === 0 && ItemID.Sets.UsesCursedByPlanteraTooltip[item.type] && !NPC.downedPlantBoss;
                add('Tooltip' + i, cursed ? Lang.tip[59].Value : tooltip.GetLine(i));
            }
        }

        if (Main.tenthAnniversaryWorld && item.type === 238) add('WizardHatDuringAnniversary', text('CommonItemTooltip.WizardHatDuringAnniversary'));
        if (Main.getGoodWorld && item.type === 1127) add('BurningBlock', text('CommonItemTooltip.BurningBlock'));

        // Mechdusa: a última linha troca de texto.
        if (Terraria.GameContent.SpecialSeedFeatures.Mechdusa) {
            if (item.type === 556 || item.type === 557 || item.type === 544) {
                lines.pop();
                add('MechSummonDuringEverything', text('CommonItemTooltip.MechSummonDuringEverything'));
            }
        } else if (item.type === 5334) {
            lines.splice(-2, 2);
            add('MechdusaSummonNotDuringEverything', text('CommonItemTooltip.MechdusaSummonNotDuringEverything'));
        }

        const dd2 = [3818, 3819, 3820, 3824, 3825, 3826, 3829, 3830, 3831, 3832, 3833, 3834];
        if (dd2.includes(item.type) && !me.downedDD2EventAnyDifficulty) add('EtherianManaWarning', Lang.misc[104].Value);

        if (item.buffType > 0 && BuffID.Sets.IsWellFed[item.buffType] && Main.expertMode) add('WellFedExpert', Lang.misc[40].Value);

        if (item.buffTime > 0) {
            add('BuffTime', item.buffTime / 60 < 60
                ? text('CommonItemTooltip.SecondDuration', Math.round(item.buffTime / 60))
                : text('CommonItemTooltip.MinuteDuration', Math.round(item.buffTime / 60 / 60)));
        }

        // Os ioiôs da One Drop: o logo, desenhado no lugar da linha.
        if ([3262, 3282, 3283, 3284, 3285, 3286, 3291, 3315, 3316, 3317, 3389].includes(item.type)) {
            add('OneDropLogo', ' ').OneDropLogo = true;
        }

        if (item.expert) add('Expert', text('GameUI.Expert'));
        if (item.rare === -13) add('Master', text('GameUI.Master'));

        if (item.prefix > 0) {
            VanillaTooltips.#PrefixLines(item, oldKB, add);
            out.prefixEnd = lines.length;
        }

        VanillaTooltips.#SetBonus(item, out, add);

        if (showShareHint && Terraria.Player.Settings.ShowLoadoutShareHint) {
            add('LoadoutShareHint', Terraria.Localization.Language.GetItemInputTextValue(item.type, 'UI.ItemLoadoutShareHint'), 'shared');
        }

        // Pesquisa (modo Jornada)
        const ctx = item.tooltipContext;
        const researchContext = (ctx >= 0 && ctx <= 7) || [15, 22, 29, 32, 34, 35, 41, 42, 43, 47, 48].includes(ctx);
        const have = new Ref(0), needed = new Ref(0), teammate = new Ref(null);
        if (researchContext && me.difficulty === 3 &&
            Main.LocalPlayerCreativeTracker.ItemSacrifices.TryGetSacrificeNumbers(item.type, have, needed)) {
            if (have.value < needed.value) {
                add('JourneyResearch', text('CommonItemTooltip.CreativeSacrificeNeeded', needed.value - have.value), 'research');
            } else if (ctx === 29 && Main.LocalPlayerCreativeTracker.ItemSacrifices.TryGetTeammateUnlockCredit(item.type, teammate)) {
                add('JourneyResearchByTeammate', text('CommonItemTooltip.ItemUnlockedByTeammate', teammate.value), 'research');
            }
        }

        const notes = item.BestiaryNotes;
        if (notes && notes !== ' ') {
            for (const note of notes.split('\n')) add('BestiaryNotes', note);
        }

        // A grade dos materiais (guia de criação no estilo de controle): um espaço, desenhado no lugar.
        if (GUIInstance.Active.GUIPageIcons.UseConsoleStyle && (ctx === 22 || ctx === 47 || ctx === 48)) {
            add('CraftingMaterials', ' ', 'materials');
        }

        return out;
    }

    // O que o prefixo mudou, comparado com o item sem ele.
    static #PrefixLines(item, oldKB, add) {
        const { Item, Lang, Main } = Terraria;
        const text = VanillaTooltips.TextValue;
        const round = VanillaTooltips.#RoundEven;
        const format = System.String['string Format(string format, object arg0)'];

        let base = Main.tooltipPrefixComparisonItem;
        if (base === null || base.type !== item.type) {
            base = Item.new();
            base['void .ctor()']();
            base['void SetDefaults(int Type, ItemVariant variant)'](item.type, null);
        }

        const line = (value, name, bad) => {
            const l = add(name, value);
            l.IsModifier = true;
            l.IsModifierBad = !!bad;
        };
        const signed = (n, suffix) => (n > 0 ? '+' + n : String(n)) + suffix;
        const percent = (now, was) => round((now - was) / was * 100);

        if (base.damage !== item.damage) {
            const n = percent(item.damage, base.damage);
            line(signed(n, Lang.tip[39].Value), 'PrefixDamage', n < 0);
        }
        if (base.useAnimation !== item.useAnimation) {
            const n = -percent(item.useAnimation, base.useAnimation);
            line(signed(n, Lang.tip[40].Value), 'PrefixSpeed', n < 0);
        }
        if (base.crit !== item.crit) {
            const n = item.crit - base.crit;
            line(signed(n, Lang.tip[41].Value), 'PrefixCritChance', n < 0);
        }
        if (base.mana !== item.mana) {
            const n = percent(item.mana, base.mana);
            line(signed(n, Lang.tip[42].Value), 'PrefixUseMana', n > 0);
        }
        if (base.scale !== item.scale) {
            const n = percent(item.scale, base.scale);
            line(signed(n, Lang.tip[43].Value), 'PrefixSize', n < 0);
        }
        if (base.shootSpeed !== item.shootSpeed) {
            const n = percent(item.shootSpeed, base.shootSpeed);
            line(signed(n, Lang.tip[44].Value), 'PrefixShootSpeed', n < 0);
        }
        if (base.knockBack !== oldKB) {
            const n = percent(oldKB, base.knockBack);
            line(signed(n, Lang.tip[45].Value), 'PrefixKnockback', n < 0);
        }
        if (base.armorPenetration !== item.armorPenetration) {
            const n = item.armorPenetration - base.armorPenetration;
            line(format(text('CommonItemTooltip.PrefixArmorPenetration'), bl.box(n, 'int')), 'PrefixArmorPenetration', n < 0);
        }
        if (base.bonusTagDamage !== item.bonusTagDamage) {
            const n = item.bonusTagDamage - base.bonusTagDamage;
            line(format(text('CommonItemTooltip.PrefixTagDamage'), bl.box(n, 'int')), 'PrefixTagDamage', n < 0);
        }

        // Acessórios: o prefixo dá um valor fixo.
        const p = item.prefix;
        if (p >= 62 && p <= 65) line('+' + (p - 61) + Lang.tip[25].Value, 'PrefixAccDefense');
        else if (p === 66) line('+20 ' + Lang.tip[31].Value, 'PrefixAccMaxMana');
        else if (p === 67 || p === 68) line('+' + (p === 67 ? 2 : 4) + Lang.tip[5].Value, 'PrefixAccCritChance');
        else if (p >= 69 && p <= 72) line('+' + (p - 68) + Lang.tip[39].Value, 'PrefixAccDamage');
        else if (p >= 73 && p <= 76) line('+' + (p - 72) + Lang.tip[46].Value, 'PrefixAccMoveSpeed');
        else if (p >= 77 && p <= 80) line('+' + (p - 76) + Lang.tip[47].Value, 'PrefixAccMeleeSpeed');
    }

    // O bônus de conjunto: o do conjunto mais perto de completo.
    static #SetBonus(item, out, add) {
        const { ArmorSetBonus, ArmorSetBonuses } = Terraria.DataStructures;
        const Main = Terraria.Main;

        let result = ArmorSetBonus.QueryResult.new();
        const context = ArmorSetBonus.QueryContext.new();
        context['void .ctor(Player player)'](Main.LocalPlayer);

        let best = null;
        for (const set of Array.from(ArmorSetBonuses.SetsContaining[item.type])) {
            if (best === null) best = set;
            const q = set.QueryCount(context);
            if (result.ItemsNeeded < q.ItemsNeeded || (result.ItemsNeeded === q.ItemsNeeded && result.ItemsFound < q.ItemsFound)) {
                result = q;
                best = set;
            }
        }
        if (best === null) return;

        const key = VanillaTooltips.TextValue(Main.ReversedUpDownArmorSetBonuses ? 'Key.UP' : 'Key.DOWN');
        if (item.wornArmor) {
            add('SetBonus', best.GetTooltipForWornArmor(context, result).replace('{0}', key), 'setBonus');
            out.setBonusColor = result.Complete ? Color.LimeGreen : Color.new(130, 130, 130);
        } else {
            add('SetBonus', best.GetTooltipForSinglePiece(item.type).replace('{0}', key), 'setBonus');
            out.setBonusColor = Color.new(130, 130, 130);
        }
    }
}
