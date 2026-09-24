#include "script/ScriptEngine.h"

#if BL_HAVE_QUICKJS
#include "core/Log.h"
#include "quickjs.h"
#include "script/Texture.h"

#include <cstdio>
#include <string>

namespace bl::script {

namespace {

/**
 * bl.readJson(caminho) — um JSON da pasta do mod de quem chama, ou undefined
 * se o arquivo nao existe. JSON quebrado lanca, com o nome do arquivo: um
 * pt-BR.json com virgula sobrando nao pode virar "o item ficou sem nome".
 */
JSValue js_readJson(JSContext* ctx, JSValueConst, int argc, JSValueConst* argv) {
    const char* rel = argc >= 1 ? JS_ToCString(ctx, argv[0]) : nullptr;
    if (!rel) return JS_ThrowTypeError(ctx, "bl.readJson(caminho)");
    const std::string path = resolveModPath(ctx, rel);
    JS_FreeCString(ctx, rel);

    FILE* f = std::fopen(path.c_str(), "rb");
    if (!f) return JS_UNDEFINED;
    std::string text;
    char buf[4096];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0) text.append(buf, n);
    std::fclose(f);
    return JS_ParseJSON(ctx, text.c_str(), text.size(), path.c_str());
}

/**
 * As classes base dos mods, no formato do ExMod (TL Pro), que e o do
 * tModLoader: o mod ESTENDE a classe e a registra.
 *
 *   export class ExampleItem extends ModItem {
 *       SetDefaults() { this.Item.maxStack = ModItem.CommonMaxStack; }
 *   }
 *   ModItem.register(ExampleItem);
 *
 * Moram no loader, e nao numa pasta TL/ de cada mod: e o mesmo codigo para
 * todos, e corrigir aqui corrige em todos. Por baixo e o bl.items.register —
 * o que muda e so a forma de escrever o item.
 *
 * Global script, nao modulo: o que ele define fica em globalThis, ao lado de
 * `Terraria` e `bl`, e os mods usam sem import.
 */
const char kModClassesJs[] = R"JS(
(() => {
'use strict';

// As culturas do jogo. O nome sai de Localization/<cultura>.json do mod.
const CULTURES = ['en-US', 'pt-BR', 'de-DE', 'it-IT', 'fr-FR', 'es-ES', 'ru-RU',
                  'zh-Hans', 'zh-Hant', 'pl-PL', 'ja-JP', 'ko-KR'];

// { 'pt-BR': 'Espada', ... } a partir de <secao>.<chave> de cada cultura.
function localized(section, key) {
    const out = {};
    let any = false;
    for (const c of CULTURES) {
        const json = bl.readJson('Localization/' + c + '.json');
        const v = json && json[section] && json[section][key];
        if (typeof v === 'string') { out[c] = v; any = true; }
    }
    return any ? out : undefined;
}

// 'Items/Espada' -> 'Textures/Items/Espada.png', como o ModTexture do ExMod.
function texturePath(texture) {
    let t = String(texture);
    if (!t.startsWith('Textures/')) t = 'Textures/' + t;
    if (!t.endsWith('.png')) t += '.png';
    return t;
}

// Os campos que o CloneDefaults copia do item do jogo (os do ItemLoader do
// ExMod, mais tamanho e som). Item.CloneDefaults nao existe nesta versao.
const CLONED_FIELDS = [
    'wornArmor', 'tooltipContext', 'BestiaryNotes', 'sentry', 'DD2Summon',
    'shopSpecialCurrency', 'expert', 'expertOnly', 'questItem', 'fishingPole', 'bait',
    'hairDye', 'makeNPC', 'dye', 'paint', 'paintCoating', 'tileWand', 'notAmmo', 'crit',
    'mech', 'reuseDelay', 'melee', 'magic', 'ranged', 'summon', 'placeStyle', 'buffTime',
    'buffType', 'mountType', 'cartTrack', 'material', 'noWet', 'vanity', 'mana', 'channel',
    'manaIncrease', 'noMelee', 'noUseGraphic', 'lifeRegen', 'shoot', 'shootSpeed',
    'shootsEveryUse', 'alpha', 'ammo', 'useAmmo', 'autoReuse', 'accessory', 'axe',
    'healMana', 'potion', 'color', 'consumable', 'createTile', 'createWall',
    'useSoundPitch', 'damage', 'defense', 'armorPenetration', 'hammer', 'healLife',
    'holdStyle', 'knockBack', 'maxStack', 'pick', 'rare', 'scale', 'tileBoost', 'useStyle',
    'useTime', 'useAnimation', 'value', 'useTurn', 'buy', 'uniqueStack', 'width', 'height',
    'UseSound',
];

const byType = new Map();

class ModItem {
    static CommonMaxStack = 9999;

    // O Item do jogo durante o SetDefaults; fora dele, undefined.
    Item = undefined;
    // O tipo (ItemID) deste item, a partir do register.
    Type = undefined;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: Localization/*.json
    // (ItemName.<Classe>), e sem isso o nome da classe.
    DisplayName = '';
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;

    SetStaticDefaults() {}
    SetDefaults(item) {}
    PostStaticDefaults() {}
    PostSetDefaults(item) {}
    // Quando todos os itens ja existem no jogo (os tipos dos outros mods tambem).
    PostSetupContent() {}

    // Copia os valores de um item do jogo para este.
    CloneDefaults(type) {
        const source = Terraria.Item.new();
        source['void .ctor()']();
        source['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        for (const key of CLONED_FIELDS) {
            try { this.Item[key] = source[key]; } catch (e) { /* campo que esta versao nao tem */ }
        }
    }

    // useTime/useAnimation, autoReuse e o useStyle que combina com o item.
    SetDefaultWeaponStyle(useTime = 30, autoReuse = false) {
        const { ItemUseStyleID } = Terraria.ID;
        this.Item.useTime = useTime;
        this.Item.useAnimation = useTime;
        this.Item.autoReuse = autoReuse;
        if (this.Item.melee) this.Item.useStyle = ItemUseStyleID.Swing;
        else if (this.Item.shoot > 0 && !this.Item.consumable) this.Item.useStyle = ItemUseStyleID.Shoot;
        else this.Item.useStyle = ItemUseStyleID.Swing;
    }

    SetWeaponValues(damage = 0, knockBack = 0, crit = 0) {
        this.Item.damage = damage;
        this.Item.knockBack = knockBack;
        this.Item.crit = crit;
    }

    SetShopValues(rarity = 0, coinValue = 0) {
        this.Item.rare = rarity;
        this.Item.value = coinValue;
    }

    DefaultToPlaceableTile(typeToPlace, styleToPlace = 0) {
        this.Item.createTile = typeToPlace;
        this.Item.placeStyle = styleToPlace;
        this.Item.useStyle = Terraria.ID.ItemUseStyleID.Swing;
        this.Item.useAnimation = 15;
        this.Item.useTime = 10;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.useTurn = true;
        this.Item.autoReuse = true;
        this.Item.consumable = true;
    }

    DefaultToFood(buffType, buffTime, useGulpSound = false, animationTime = 17) {
        const { SoundID } = Terraria.ID;
        this.Item.useStyle = useGulpSound ? 9 : 2;
        this.Item.UseSound = useGulpSound ? SoundID.Item3 : SoundID.Item2;
        this.Item.useTurn = true;
        this.Item.useTime = this.Item.useAnimation = animationTime;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.consumable = true;
        this.Item.buffType = buffType;
        this.Item.buffTime = buffTime;
        this.Item.rare = 1;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 20, 0);
    }

    static sellPrice(platinum = 0, gold = 0, silver = 0, copper = 0) {
        return Terraria.Item.sellPrice(platinum, gold, silver, copper);
    }
    static buyPrice(platinum = 0, gold = 0, silver = 0, copper = 0) {
        return Terraria.Item.buyPrice(platinum, gold, silver, copper);
    }

    /**
     * Instancia a classe e registra o item. Uma instancia por classe: o
     * SetDefaults dela roda para cada Item desse tipo que o jogo criar, com
     * this.Item apontando para ele. Devolve o tipo.
     */
    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModItem)) {
            throw new TypeError('ModItem.register(Classe): passe a classe, que estende ModItem');
        }
        const inst = new cls();
        const name = cls.name;
        const type = bl.items.register({
            name,
            texture: texturePath(inst.Texture || name),
            displayName: inst.DisplayName || localized('ItemName', name) || name,
            setDefaults(item) {
                inst.Item = item;
                try {
                    inst.SetDefaults(item);
                    inst.PostSetDefaults(item);
                } finally {
                    inst.Item = undefined;
                }
            },
            setStaticDefaults() {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                inst.PostSetupContent();
            },
        });
        inst.Type = type;
        byType.set(type, inst);
        return type;
    }

    static isModType(type) { return bl.items.isModItem(type); }
    static isModItem(item) { return !!item && bl.items.isModItem(item.type); }
    // O tipo de um item DESTE mod pelo nome da classe; -1 se nao ha.
    static getTypeByName(name) { return bl.items.typeOf(name); }
    // A instancia registrada para o tipo, ou undefined.
    static getModItem(type) { return byType.get(type); }
    static getByName(name) { return byType.get(bl.items.typeOf(name)); }
}

globalThis.ModItem = ModItem;
})();
)JS";

} // namespace

void installModClasses(void* context) {
    auto* ctx = static_cast<JSContext*>(context);
    JSValue global = JS_GetGlobalObject(ctx);
    JSValue bl = JS_GetPropertyStr(ctx, global, "bl");
    JS_SetPropertyStr(ctx, bl, "readJson", JS_NewCFunction(ctx, js_readJson, "readJson", 1));
    JS_FreeValue(ctx, bl);
    JS_FreeValue(ctx, global);

    JSValue r = JS_Eval(ctx, kModClassesJs, sizeof(kModClassesJs) - 1, "bunny:ModClasses.js",
                        JS_EVAL_TYPE_GLOBAL);
    if (JS_IsException(r)) {
        JSValue e = JS_GetException(ctx);
        const char* t = JS_ToCString(ctx, e);
        BL_ERROR("classes dos mods (ModItem) nao carregaram: %s", t ? t : "?");
        if (t) JS_FreeCString(ctx, t);
        JS_FreeValue(ctx, e);
    } else {
        BL_INFO("classes dos mods instaladas (ModItem)");
    }
    JS_FreeValue(ctx, r);
}

} // namespace bl::script

#else
namespace bl::script {
void installModClasses(void*) {}
} // namespace bl::script
#endif
