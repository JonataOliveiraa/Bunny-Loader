const Main = Terraria.Main;
const counts = new Map();
let testing = false, completed = false, frames = 0, failures = 0;
let extraCrit = 0, speed = 1, manaMult = 1, preventItem = false, preventPotion = false, potionDelay = 0;
let fishingBonus = 0, craftMaterial = null;
const craftConsumed = [];
const P = Terraria.Player;

export default class ModPlayerHooksTest extends Mod {}

export class HooksProbe extends ModPlayer {
    CopyClientState(player, target) { target.marker = this.marker; }
    ModifyWeaponCrit(player, item, crit) { crit.value += extraCrit; }
    UseSpeedMultiplier() { return speed; }
    ModifyManaCost(player, item, reduce, mult) { mult.value *= manaMult; }
    PreItemCheck() { return !preventItem; }
    ApplyPotionDelay(player, item, delay) { potionDelay = delay; return !preventPotion; }
    ModifyFishingAttempt(player, attempt) { counts.set('ModifyFishingAttempt', (counts.get('ModifyFishingAttempt') || 0) + 1); }
    GetFishingLevel(player, rod, bait, level) { level.value += fishingBonus; }
    AddMaterialsForCrafting(player, callback) {
        callback.value = (item, index) => craftConsumed.push([bl.addressOf(item), index]);
        return craftMaterial ? [craftMaterial] : null;
    }
}

for (const name of Object.getOwnPropertyNames(ModPlayer.prototype)) {
    if (name === 'constructor' || name === 'Player' || Object.hasOwn(HooksProbe.prototype, name)) continue;
    HooksProbe.prototype[name] = function (...args) {
        counts.set(name, (counts.get(name) || 0) + 1);
        return ModPlayer.prototype[name].apply(this, args);
    };
}

function check(name, run) {
    try {
        if (run() === false) throw new Error('resultado falso');
        bl.log('modplayerhooks ' + name + ': ok');
    } catch (error) { failures++; bl.log('modplayerhooks ' + name + ': FALHOU ' + error); }
}

function sample(type) {
    const item = Terraria.Item.new();
    item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
}

function run(player) {
    const sword = sample(Terraria.ID.ItemID.CopperShortsword), potion = sample(Terraria.ID.ItemID.LesserHealingPotion);
    check('estado por jogador', () => HooksProbe.get(player) !== HooksProbe.get(Main.player[(Main.myPlayer + 1) % 255]));
    check('critico por referencia', () => {
        extraCrit = 0;
        const before = player['int GetWeaponCrit(Item sItem)'](sword);
        extraCrit = 12;
        const after = player['int GetWeaponCrit(Item sItem)'](sword);
        extraCrit = 0;
        return after === before + 12;
    });
    check('tempo de uso e animacao', () => {
        const previous = [player.itemTime, player.itemTimeMax, player.itemAnimation, player.itemAnimationMax];
        try {
            speed = 1;
            player['void ApplyItemTime(Item sItem)'](sword);
            const before = player.itemTime;
            speed = 2;
            player['void ApplyItemTime(Item sItem)'](sword);
            return player.itemTime === Math.max(1, Math.trunc(before / 2));
        } finally {
            speed = 1;
            [player.itemTime, player.itemTimeMax, player.itemAnimation, player.itemAnimationMax] = previous;
        }
    });
    check('mana sem modificar custo permanente', () => {
        const previous = [player.statMana, player.manaCost, player.slowMagicUse];
        try {
            player.statMana = 100;
            player.manaCost = 1;
            manaMult = 0.5;
            player['bool CheckMana(int amount, bool pay, bool blockQuickMana)'](20, true, true);
            return player.statMana === 90 && player.manaCost === 1;
        } finally {
            manaMult = 1;
            [player.statMana, player.manaCost, player.slowMagicUse] = previous;
        }
    });
    check('veto de ItemCheck', () => {
        const before = counts.get('PostItemCheck') || 0;
        preventItem = true;
        try { player['void ItemCheck()'](); }
        finally { preventItem = false; }
        return counts.get('PostItemCheck') === before + 1;
    });
    check('veto de PotionDelay e valor real', () => {
        const before = player.potionDelay;
        preventPotion = true;
        try {
            player['void ApplyPotionDelay(Item sItem)'](potion);
            return player.potionDelay === before && potionDelay > 0;
        } finally { preventPotion = false; player.potionDelay = before; }
    });
    check('nivel de pesca por referencia e estrutura nativa', () => {
        const before = player['PlayerFishingConditions GetFishingConditions()']().FinalFishingLevel;
        fishingBonus = 15;
        try { return player['PlayerFishingConditions GetFishingConditions()']().FinalFishingLevel === before + 15; }
        finally { fishingBonus = 0; }
    });
    check('clone com estado separado', () => {
        const instance = HooksProbe.get(player);
        instance.marker = 21;
        const clone = player['Player clientClone()']();
        const copy = HooksProbe.get(clone);
        return copy !== instance && copy.marker === 21;
    });
    const Recipe = Terraria.Recipe;
    check('materiais extras na lista nativa de receitas', () => {
        Recipe['void FindRecipes(bool canDelayCheck)'](false);
        const before = Recipe._recipeSources.Count;
        craftMaterial = sample(Terraria.ID.ItemID.StoneBlock);
        craftMaterial.stack = 3;
        Recipe['void FindRecipes(bool canDelayCheck)'](false);
        return Recipe._recipeSources.Count === before + 1;
    });
    for (const amount of [1, 2]) check('consumo de crafting ' + (amount === 1 ? 'parcial' : 'total'), () => {
        const sources = Recipe._recipeSources, storage = sources.get_Item(sources.Count - 1).storage;
        const consumed = amount === 2 ? bl.classOf('System.Collections.Generic', 'List`1').makeGeneric(Terraria.Item).new() : null;
        if (consumed) consumed['void .ctor()']();
        const requirement = Terraria.Recipe.RequiredItemEntry.new();
        requirement['void .ctor(int itemIdOrRecipeGroup, int stack)'](craftMaterial.type, amount);
        const remaining = new Ref(amount), before = craftMaterial.stack, type = craftMaterial.type, count = craftConsumed.length;
        Terraria.GameContent.CraftingRequests['void ConsumeItemsFrom(Item[] inventory, int maxItems, RequiredItemEntry req, ref int toConsume, List`1 consumedItems, int chestIndex)'](
            storage.item, storage.maxItems, requirement, remaining, consumed, -1);
        return remaining.value === 0 && craftMaterial.stack === before - amount && craftConsumed.length === count + 1 &&
            craftConsumed[count][0] === bl.addressOf(craftMaterial) && craftConsumed[count][1] === 0 &&
            (!consumed || consumed.Count === 1 && consumed.get_Item(0).type === type && consumed.get_Item(0).stack === amount);
    });
    craftMaterial = null;
    Recipe['void FindRecipes(bool canDelayCheck)'](false);
}

P['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (completed || testing || Main.gameMenu || index !== Main.myPlayer) return;
    frames++;
    if (frames === 45) {
        testing = true;
        try { run(player); }
        finally { testing = false; }
    }
    if (frames === 90) {
        for (const name of ['PreUpdateMovement', 'PostUpdateMiscEffects', 'PostUpdateRunSpeeds', 'NaturalLifeRegen',
            'DrawEffects', 'ModifyDrawInfo', 'HideDrawLayers', 'ModifyDrawLayerOrdering', 'TransformDrawData', 'ModifyScreenPosition', 'ModifyZoom']) {
            check('callback ' + name, () => (counts.get(name) || 0) > 0);
        }
        completed = true;
        bl.log('modplayerhooks FIM falhas=' + failures + ' callbacks=' + counts.size);
    }
});
