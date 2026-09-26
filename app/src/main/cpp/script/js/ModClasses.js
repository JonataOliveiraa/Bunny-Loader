// As classes base dos mods: ModItem, ModProjectile, ModNPC (e ModRecipe,
// NPCLoot, NPCSpawnInfo). Formato do ExMod (TL Pro), que e o do tModLoader:
// o mod ESTENDE a classe e a registra.
//
// Embutido na libbunny (CMakeLists: configure_file) e avaliado no escopo
// global depois dos bindings — ver ModClasses.cpp. Nada de arroba neste
// arquivo: o configure_file trocaria o que estiver entre duas.
//
// Hooks: cada metodo do jogo so e hookado quando alguma classe registrada
// sobrescreve o metodo correspondente, e com filtro NATIVO pelo tipo (ver
// HookFilter em Bridge.h) — NPC, projetil e item do jogo nem entram no JS.
(() => {
'use strict';

const FIRST_ITEM = bl.items.vanillaCount;
const FIRST_PROJECTILE = bl.projectiles.vanillaCount;
const FIRST_NPC = bl.npcs.vanillaCount;
const FIRST_BUFF = bl.buffs.vanillaCount;
const FIRST_TILE = bl.tiles.vanillaCount;

// As culturas do jogo. Nome e tooltip saem de Localization/<cultura>.json do mod.
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

// A classe sobrescreveu o metodo da base?
function overrides(cls, base, name) {
    return cls.prototype[name] !== base.prototype[name];
}

// Um hook por metodo do jogo, instalado na primeira classe que precisa dele.
const installed = new Set();
function once(key, install) {
    if (installed.has(key)) return;
    installed.add(key);
    install();
}

// Um erro do mod num hook de todo quadro nao pode virar uma enxurrada: loga a
// primeira vez por metodo e segue.
const reported = new Set();
function guard(label, fn) {
    try {
        return fn();
    } catch (e) {
        if (!reported.has(label)) {
            reported.add(label);
            bl.log('erro em ' + label + ': ' + e + (e && e.stack ? '\n' + e.stack : ''));
        }
        return undefined;
    }
}

// O que roda quando todo o conteudo de mod ja esta no jogo (receitas do
// jogo montadas, Bestiario criado): receitas, Bestiario, PostSetupContent.
// Os grupos de receita ('groups') rodam antes de tudo: uma receita pode usar
// o grupo de outro mod.
const readyTasks = { groups: [], content: [] };
let readyHooked = false;
function whenReady(task, phase = 'content') {
    readyTasks[phase].push(task);
    if (readyHooked) return;
    readyHooked = true;
    bl.onContentReady(() => {
        for (const t of readyTasks.groups) guard('AddRecipeGroups', t);
        for (const t of readyTasks.content) guard('PostSetupContent', t);
        finishBestiary();
        finishRecipes();
    });
}

// ========================= instancia por entidade =========================
//
// Como no tModLoader: a classe registrada e o MOLDE (um por tipo), e cada
// Item, Projectile e NPC do jogo desse tipo ganha a PROPRIA instancia, copiada
// do molde (Clone), em `item.ModItem`, `proj.ModProjectile` e `npc.ModNPC`.
// Dentro dela, `this.Item` (`this.Projectile`, `this.NPC`) e aquela entidade.
//
// O campo mora ao lado do objeto do jogo (bl.defineField: o IL2CPP nao deixa
// crescer a classe), e a instancia guarda o ENDERECO da entidade, nao o
// objeto: nada do lado do mod segura vivo um item que o jogo ja descartou.

const definedFields = new Set();
function defineEntityField(cls, field) {
    if (definedFields.has(field)) return;
    definedFields.add(field);
    bl.defineField(cls, field);
}

// O modelo de cada classe registrada (ModItem, ModNPC...): o do
// ModContent.GetInstance. E `this.Mod`, como no tModLoader: o Mod de quem
// registrou, que vai junto nas copias.
const contentByClass = new Map();
function adoptTemplate(cls, inst) {
    inst.Mod = bl.mod;
    contentByClass.set(cls, inst);
}

// Fora do Mod Menu: `HideFromModMenu`, ou o jeito do tModLoader que o jogo
// tambem entende (`fromGame`). Depois do SetStaticDefaults, onde o mod pede.
function applyMenuVisibility(kind, inst, type, fromGame) {
    const hide = inst.HideFromModMenu || guard(inst.constructor.name + ' (Mod Menu)', fromGame);
    if (hide) bl.menu.hide(kind, type);
}

function bindInstance(inst, entity, field) {
    inst.__entity = bl.addressOf(entity);
    entity[field] = inst;
    return inst;
}

// A instancia da entidade. Sem ela (entidade que nao passou pelo SetDefaults
// com o mod carregado), uma copia do molde, na hora.
function instanceOf(entity, field, byType) {
    if (!entity) return undefined;
    const type = entity.type;
    const template = byType.get(type);
    if (!template) return undefined;
    const current = entity[field];
    if (current && current.Type === type) return current;
    return bindInstance(template.Clone(entity), entity, field);
}

// O MemberwiseClone do C#: os mesmos campos, rasos, noutra instancia.
function cloneInstance(inst) {
    const c = Object.create(Object.getPrototypeOf(inst));
    Object.assign(c, inst);
    c.__entity = 0;
    return c;
}

function entityOf(inst) {
    return inst.__entity ? bl.objectAt(inst.__entity) : undefined;
}

// ============================= ModLocalization =============================

// O texto na cultura do jogo agora: { 'pt-BR': ..., 'en-US': ... } -> texto.
function pickCulture(map) {
    if (typeof map === 'string') return map;
    let now = '';
    try { now = Terraria.Localization.Language.ActiveCulture.Name; } catch (e) { /* sem idioma ainda */ }
    return map[now] ?? map['en-US'] ?? map[''] ?? Object.values(map)[0] ?? '';
}

/**
 * Chaves de idioma do jogo com texto do mod, como o ModLocalization do ExMod.
 * O Bestiario (FlavorTextBestiaryInfoElement) e outras partes do jogo pedem
 * uma CHAVE, e nao o texto: Translate registra o texto de
 * Localization/<cultura>.json do mod sob uma chave e devolve a chave.
 */
class ModLocalization {
    // 'Bestiary.ExampleSlimeNPC' -> a chave registrada (ou o proprio caminho, sem texto).
    static Translate(path) {
        const dot = path.indexOf('.');
        const map = dot > 0 ? localized(path.slice(0, dot), path.slice(dot + 1)) : undefined;
        if (!map) return path;
        return ModLocalization.Register('Mods.' + path, map);
    }

    // O TEXTO (na lingua do jogo) de 'NPCChat.ExamplePerson_1', para o que o
    // jogo pede como texto (a fala do morador). Sem texto, o proprio caminho.
    static GetTextValue(path) {
        const dot = path.indexOf('.');
        const map = dot > 0 ? localized(path.slice(0, dot), path.slice(dot + 1)) : undefined;
        return map ? pickCulture(map) : path;
    }

    // Registra `text` (texto ou { cultura: texto }) sob `key`. Devolve a chave.
    static Register(key, text) {
        const value = pickCulture(text);
        const texts = Terraria.Localization.LanguageManager.Instance._localizedTexts;
        const lt = Terraria.Localization.LocalizedText.new();
        lt['void .ctor(string key, string text)'](key, value);
        texts['void set_Item(string key, LocalizedText value)'](key, lt);
        return key;
    }
}

// ================================ ModItem ================================

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

const itemsByType = new Map();

// Animacao de item (Main.RegisterItemAnimation): o jogo zera todas no
// InitializeItemAnimations, que roda DEPOIS do SetStaticDefaults dos mods.
// Guardadas aqui e reaplicadas a cada vez que ele roda.
const itemAnimations = new Map();
let animationsHooked = false;
function registerItemAnimation(type, animation) {
    itemAnimations.set(type, animation);
    Terraria.Main['void RegisterItemAnimation(int index, DrawAnimation animation)'](type, animation);
    if (animationsHooked) return;
    animationsHooked = true;
    Terraria.Main['void InitializeItemAnimations()'].hook((original) => {
        original();
        for (const [t, a] of itemAnimations) {
            guard('RegisterItemAnimation', () =>
                Terraria.Main['void RegisterItemAnimation(int index, DrawAnimation animation)'](t, a));
        }
    });
}
const itemOf = (item) => instanceOf(item, 'ModItem', itemsByType);

class ModItem {
    static CommonMaxStack = 9999;

    // O Item do jogo DESTA instancia (no molde, undefined). Ver "instancia por
    // entidade", acima.
    get Item() { return entityOf(this); }
    // A instancia de uma entidade nova, a partir desta (o molde, ou a de um
    // item que o jogo copiou). Sobrescreva para copiar fundo o que for seu.
    Clone(newItem) { return cloneInstance(this); }
    // O tipo (ItemID) deste item, a partir do register.
    Type = undefined;
    // true: fora do Mod Menu (o jeito do tModLoader tambem vale:
    // ItemID.Sets.Deprecated[this.Type] = true no SetStaticDefaults).
    HideFromModMenu = false;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: ItemName.<Classe> em
    // Localization/*.json, e sem isso o nome da classe.
    DisplayName = '';
    // Idem, ItemTooltip.<Classe>. Linhas separadas por \n.
    Tooltip = '';
    // As linhas do tooltip, para o ModifyTooltipLines mexer.
    TooltipLines = [];
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;

    SetStaticDefaults() {}
    SetDefaults(item) {}
    PostStaticDefaults() {}
    PostSetDefaults(item) {}
    // Quando todo o conteudo de mod ja esta no jogo.
    PostSetupContent() {}
    // Uma vez por cultura, com this.TooltipLines ja preenchido.
    ModifyTooltipLines() {}
    // Na hora de mostrar: as linhas do tooltip (TooltipLine) — mude o texto,
    // a cor (OverrideColor), insira e tire linhas. Tags [c/RRGGBB:texto] no
    // texto pintam trechos.
    ModifyTooltips(item, tooltips) {}
    // Uma vez, antes de qualquer receita: ModRecipe.CreateRecipeGroup(...).
    AddRecipeGroups() {}
    // Uma vez, quando as receitas do jogo ja existem: this.CreateRecipe(...).
    AddRecipes() {}
    // Ao criar este item no menu de criacao; `item` e o que o jogador vai
    // receber (ainda da para mudar).
    OnCraft(item, player, recipe) {}

    // false impede o uso.
    CanUseItem(item, player) { return true; }
    // A cada uso (no quadro em que o uso comeca).
    UseItem(item, player) {}
    // Todo quadro com o item na mao, usando ou nao.
    HoldItem(item, player) {}
    // Todo quadro de uso / de segurar, depois do estilo do jogo.
    UseStyle(item, player, mountOffset, heldItemFrame) {}
    HoldStyle(item, player, mountOffset, heldItemFrame) {}
    // Desloca a arma na mao: { X, Y } em pixels, ou undefined.
    HoldoutOffset(item, player) { return undefined; }
    // false: nao atira (o uso acontece, sem projetil).
    CanShoot(item, player) { return true; }
    // Antes de cada projetil do tiro: mude stats.position/velocity/type/damage/knockBack.
    ModifyShootStats(item, player, stats) {}
    // false: o projetil do jogo nao nasce (crie os seus aqui).
    Shoot(item, player, position, velocity, type, damage, knockBack) { return true; }
    // Acerto corpo a corpo.
    OnHitNPC(item, player, npc, damageDone, knockBack, crit) {}
    // Equipado (armadura ou acessorio), todo quadro.
    UpdateEquip(item, player) {}
    // Acessorio equipado, todo quadro.
    // vanity: slot de vaidade (so visual). hideVisual: o olho do slot fechado.
    UpdateAccessory(item, player, vanity, hideVisual) {}
    // No inventario, todo quadro.
    UpdateInventory(item, player) {}
    // No chao: a cor com que o item e desenhado. Devolva uma Color (ou nada,
    // para a do jogo). `item` e o WorldItem; a luz do lugar chega em lightColor.
    GetAlpha(item, lightColor) { return undefined; }
    // Vara de pesca na mao, a cada boia. Como no tModLoader: dois Ref.
    // lineOriginOffset.value e de onde a linha sai, em pixels a partir do
    // centro do jogador olhando para a direita; lineColor.value, a cor (a
    // linha colorida do jogador ganha). O jeito antigo, com tres parametros
    // (item, bobber, line) e line.lineOriginOffset / line.lineColor, continua.
    ModifyFishingLine(item, bobber, lineOriginOffset, lineColor) {}

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

    // Quadros do item (tira vertical), como a alma: ticksPerFrame por quadro.
    // Vale no inventario, no chao e na mao.
    SetItemAnimation(frameCount, ticksPerFrame = 5, pingPong = false) {
        const anim = Terraria.DataStructures.DrawAnimationVertical.new();
        anim['void .ctor(int ticksperframe, int frameCount, bool pingPong)'](ticksPerFrame, frameCount, pingPong);
        registerItemAnimation(this.Type, anim);
        return anim;
    }

    // Chicote e lanca, com os numeros de uso do jogo.
    DefaultToWhip(projType, damage, knockBack, shootSpeed, animationTime = 30) {
        this.Item['void DefaultToWhip(int projectileId, int dmg, float kb, float shootspeed, int animationTotalTime)'](
            projType, damage, knockBack, shootSpeed, animationTime);
    }
    DefaultToSpear(projType, pushForwardSpeed, animationTime) {
        this.Item['void DefaultToSpear(int projType, float pushForwardSpeed, int animationTime)'](
            projType, pushForwardSpeed, animationTime);
    }

    // Bola de golfe: tee, taco e o projetil `projType`, como as do jogo.
    DefaultToGolfBall(projType) {
        this.Item['void DefaultToGolfBall(int projid)'](projType);
    }

    // Receita que da este item: this.CreateRecipe(stack).AddIngredient(...).AddTile(...).Register().
    CreateRecipe(stack = 1) {
        return new ModRecipe().SetResult(this.Type, stack);
    }

    // Grupo com o nome do primeiro item ("Qualquer <nome>"), como no ExMod.
    CreateRecipeGroup(itemTypes = []) {
        const name = Terraria.Lang['LocalizedText GetItemName(int id)'](itemTypes[0]).Value;
        return ModRecipe.CreateRecipeGroup(name, itemTypes);
    }

    static sellPrice(platinum = 0, gold = 0, silver = 0, copper = 0) {
        return Terraria.Item.sellPrice(platinum, gold, silver, copper);
    }
    static buyPrice(platinum = 0, gold = 0, silver = 0, copper = 0) {
        return Terraria.Item.buyPrice(platinum, gold, silver, copper);
    }

    /**
     * Instancia a classe (o molde) e registra o item. Cada Item desse tipo
     * que o jogo criar ganha uma copia do molde em item.ModItem, e o
     * SetDefaults roda nela, com this.Item apontando para ele. Devolve o tipo.
     */
    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModItem)) {
            throw new TypeError('ModItem.register(Classe): passe a classe, que estende ModItem');
        }
        autoloadGores();
        defineEntityField(Terraria.Item, 'ModItem');
        const inst = new cls();
        adoptTemplate(cls, inst);
        const name = cls.name;
        const type = bl.items.register({
            name,
            texture: texturePath(inst.Texture || name),
            displayName: inst.DisplayName || localized('ItemName', name) || name,
            setDefaults(item) {
                const m = bindInstance(inst.Clone(item), item, 'ModItem');
                m.SetDefaults(item);
                m.PostSetDefaults(item);
            },
            setStaticDefaults() {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                applyMenuVisibility('item', inst, inst.Type, () => Terraria.ID.ItemID.Sets.Deprecated[inst.Type]);
            },
        });
        inst.Type = type;
        itemsByType.set(type, inst);

        setupTooltip(inst, name, type);
        whenReady(() => inst.AddRecipeGroups(), 'groups');
        whenReady(() => {
            inst.AddRecipes();
            inst.PostSetupContent();
        });
        hookItem(cls);
        once('item.Clone', hookItemClone);
        return type;
    }

    static isModType(type) { return bl.items.isModItem(type); }
    static isModItem(item) { return !!item && bl.items.isModItem(item.type); }
    // O tipo de um item DESTE mod pelo nome da classe; -1 se nao ha.
    static getTypeByName(name) { return bl.items.typeOf(name); }
    // A instancia registrada para o tipo, ou undefined.
    static getModItem(type) { return itemsByType.get(type); }
    static getByName(name) { return itemsByType.get(bl.items.typeOf(name)); }
}

// O tooltip por cultura: Tooltip da instancia ou ItemTooltip.<Classe>, e o
// ModifyTooltipLines por cima.
function setupTooltip(inst, name, type) {
    let base = inst.Tooltip || localized('ItemTooltip', name) || '';
    if (typeof base === 'string') base = base ? { '': base } : {};
    const cultures = Object.keys(base);
    if (!cultures.length && overrides(inst.constructor, ModItem, 'ModifyTooltipLines')) cultures.push('');
    const out = {};
    let any = false;
    for (const c of cultures) {
        inst.TooltipLines = base[c] ? String(base[c]).split('\n') : [];
        guard(name + '.ModifyTooltipLines', () => inst.ModifyTooltipLines());
        if (inst.TooltipLines.length) {
            out[c] = inst.TooltipLines.join('\n');
            any = true;
        }
    }
    inst.TooltipLines = [];
    if (any) bl.items.setTooltip(type, out);
}

// O Item do tiro de agora e quem atira: o NewProjectile que o
// ItemCheck_Shoot do jogo chamar e deste tiro.
let shooting = null;

function hookItem(cls) {
    const P = Terraria.Player;
    const has = (name) => overrides(cls, ModItem, name);
    const onItem = (param) => ({ minType: FIRST_ITEM, on: param });

    if (has('CanUseItem')) once('item.CanUse', () => {
        P['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'].hook((original, self, item, ignoreCursed) => {
            const m = itemOf(item);
            if (m && guard(m.constructor.name + '.CanUseItem', () => m.CanUseItem(item, self)) === false) return false;
            return original(self, item, ignoreCursed);
        }, onItem(0));
    });

    if (has('OnCraft')) once('item.OnCraft', () => {
        Terraria.Main['void CraftItem_GrantItem(Recipe recipe, Item result, bool quickCraft)'].hook(
            (original, recipe, result, quickCraft) => {
                const m = itemOf(result);
                if (m) guard(m.constructor.name + '.OnCraft', () => m.OnCraft(result, Terraria.Main.player[Terraria.Main.myPlayer], recipe));
                original(recipe, result, quickCraft);
            }, onItem(1));
    });

    if (has('UseItem')) once('item.Use', () => {
        P['void ItemCheck_StartActualUse(Item sItem)'].hook((original, self, item) => {
            original(self, item);
            const m = itemOf(item);
            if (m) guard(m.constructor.name + '.UseItem', () => m.UseItem(item, self));
        }, onItem(0));
    });

    const styles = [['ItemCheck_ApplyUseStyle', 'UseStyle'], ['ItemCheck_ApplyHoldStyle', 'HoldStyle']];
    if (has('UseStyle') || has('HoldStyle') || has('HoldoutOffset') || has('HoldItem')) {
        for (const [method, hookName] of styles) once('item.' + hookName, () => {
            P['void ' + method + '(float mountOffset, Item sItem, Rectangle heldItemFrame)'].hook(
                (original, self, mountOffset, item, frame) => {
                    original(self, mountOffset, item, frame);
                    const m = itemOf(item);
                    if (!m) return;
                    const n = m.constructor.name;
                    guard(n + '.HoldoutOffset', () => applyHoldout(m.HoldoutOffset(item, self), self));
                    guard(n + '.' + hookName, () => m[hookName](item, self, mountOffset, frame));
                    guard(n + '.HoldItem', () => m.HoldItem(item, self));
                }, onItem(1));
        });
    }

    if (has('CanShoot') || has('ModifyShootStats') || has('Shoot')) once('item.Shoot', () => {
        P['void ItemCheck_Shoot(int i, Item sItem, int weaponDamage, bool withAudioVisualFeedback)'].hook(
            (original, self, i, item, damage, feedback) => {
                const m = itemOf(item);
                if (!m) return original(self, i, item, damage, feedback);
                if (guard(m.constructor.name + '.CanShoot', () => m.CanShoot(item, self)) === false) {
                    self['void ApplyItemTime(Item sItem)'](item);
                    return undefined;
                }
                const outer = shooting;
                shooting = { m, item, player: self };
                try {
                    return original(self, i, item, damage, feedback);
                } finally {
                    shooting = outer;
                }
            }, onItem(1));
        Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'].hook(
            (original, source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier) => {
                const s = shooting;
                if (!s) return original(source, x, y, sx, sy, type, damage, knockBack, owner, ai0, ai1, ai2, modifier);
                shooting = null;   // o projetil que ELE criar nao e mais do tiro
                try {
                    const n = s.m.constructor.name;
                    // Vector2 do jogo: o Shoot pode repassa-los ao NewProjectile.
                    const stats = { position: Vector2.new(x, y), velocity: Vector2.new(sx, sy), type, damage, knockBack };
                    guard(n + '.ModifyShootStats', () => s.m.ModifyShootStats(s.item, s.player, stats));
                    const go = guard(n + '.Shoot', () => s.m.Shoot(s.item, s.player, stats.position, stats.velocity,
                                                                    stats.type, stats.damage, stats.knockBack));
                    // 1000 e o "sem vaga" do jogo: Main.projectile[1000] e o vazio.
                    if (go === false) return 1000;
                    return original(source, stats.position.X, stats.position.Y, stats.velocity.X, stats.velocity.Y,
                                    stats.type, stats.damage, stats.knockBack, owner, ai0, ai1, ai2, modifier);
                } finally {
                    shooting = s;
                }
            });
    });

    if (has('OnHitNPC')) once('item.OnHitNPC', () => {
        P['void ApplyNPCOnHitEffects(Item sItem, Rectangle itemRectangle, int damage, float knockBack, int npcIndex, int dmgRandomized, int dmgDone)'].hook(
            (original, self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone) => {
                original(self, item, rect, damage, knockBack, npcIndex, dmgRandomized, dmgDone);
                const m = itemOf(item);
                if (m) guard(m.constructor.name + '.OnHitNPC', () => m.OnHitNPC(item, self, Terraria.Main.npc[npcIndex],
                                                                                 dmgDone, knockBack, dmgDone >= dmgRandomized * 2));
            }, onItem(0));
    });

    if (has('UpdateEquip') || has('UpdateAccessory')) {
        once('item.Accessory', () => {
            P['void ApplyEquipFunctional(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                original(self, slot, item);
                const m = itemOf(item);
                if (!m) return;
                guard(m.constructor.name + '.UpdateEquip', () => m.UpdateEquip(item, self));
                if (item.accessory) {
                    const hide = !!self.hideVisibleAccessory[slot];
                    guard(m.constructor.name + '.UpdateAccessory', () => m.UpdateAccessory(item, self, false, hide));
                }
            }, onItem(1));
        });
        if (has('UpdateAccessory')) once('item.AccessoryVanity', () => {
            P['void ApplyEquipVanity(int itemSlot, Item currentItem)'].hook((original, self, slot, item) => {
                original(self, slot, item);
                const m = itemOf(item);
                if (m && item.accessory) {
                    guard(m.constructor.name + '.UpdateAccessory', () => m.UpdateAccessory(item, self, true, false));
                }
            }, onItem(1));
        });
        once('item.Armor', () => {
            P['void GrantArmorBenefits(Item armorPiece)'].hook((original, self, item) => {
                original(self, item);
                const m = itemOf(item);
                if (m) guard(m.constructor.name + '.UpdateEquip', () => m.UpdateEquip(item, self));
            }, onItem(0));
        });
    }

    // WorldItem.type e propriedade, nao campo: sem filtro nativo. So entra quem
    // escreveu GetAlpha, e o item do chao do jogo sai no primeiro `if`.
    if (has('GetAlpha')) once('item.GetAlpha', () => {
        Terraria.WorldItem['Color GetAlpha(Color newColor)'].hook((original, self, color) => {
            const m = itemsByType.has(self.type) ? itemOf(self.inner) : undefined;
            if (!m || !overrides(m.constructor, ModItem, 'GetAlpha')) return original(self, color);
            const c = guard(m.constructor.name + '.GetAlpha', () => m.GetAlpha(self, color));
            return original(self, c || color);
        });
    });

    if (has('ModifyTooltips')) once('item.Tooltips', hookTooltips);

    if (has('ModifyFishingLine')) once('item.FishingLine', hookFishingLine);

    if (has('UpdateInventory')) once('item.Inventory', () => {
        P['void UpdateEquips(int i)'].hook((original, self, i) => {
            original(self, i);
            for (const item of bl.items.modItemsIn(self)) {
                const m = itemOf(item);
                if (m) guard(m.constructor.name + '.UpdateInventory', () => m.UpdateInventory(item, self));
            }
        });
    });
}

// A linha da vara. O Main.DrawProj_FishingLine poe a ponta da linha no
// mountedCenter e so a desloca para as varas do jogo (um switch pelo tipo): a
// de mod saia do centro do jogador. O deslocamento entra pelo mountedCenter,
// na conta do ItemLoader.ModifyFishingLine do tModLoader; a cor, pelo
// TryApplyingPlayerStringColor, que recebe a da vara antes da do jogador.
function hookFishingLine() {
    const Main = Terraria.Main;
    const draw = Main['void DrawProj_FishingLine(Projectile proj, Player theOwner, ref float polePosX, ref float polePosY, Vector2 mountedCenter)'];
    let lineColor = null;
    draw.hook((original, proj, owner, polePosX, polePosY, center) => {
        const item = owner.inventory[owner.selectedItem];
        const m = item ? itemOf(item) : undefined;
        if (!m) return original();
        // Como o ItemLoader do tModLoader: o deslocamento e a cor por Ref. Com
        // tres parametros, o jeito antigo: um objeto com os dois campos.
        const name = m.constructor.name + '.ModifyFishingLine';
        const line = { lineOriginOffset: Vector2.new(0, 0), lineColor: Color.new(200, 200, 200, 100) };
        if (m.ModifyFishingLine.length === 3) {
            guard(name, () => m.ModifyFishingLine(item, proj, line));
        } else {
            const offsetRef = new Ref(line.lineOriginOffset);
            const colorRef = new Ref(line.lineColor);
            guard(name, () => m.ModifyFishingLine(item, proj, offsetRef, colorRef));
            line.lineOriginOffset = offsetRef.value;
            line.lineColor = colorRef.value;
        }
        const offset = line.lineOriginOffset || { X: 0, Y: 0 };
        const dir = owner.direction;
        const x = center.X + offset.X * dir - (dir < 0 ? 13 : 0);
        const y = center.Y + offset.Y * owner.gravDir;
        const outer = lineColor;
        lineColor = line.lineColor || null;
        try {
            return original(proj, owner, polePosX, polePosY, Vector2.new(x, y));
        } finally {
            lineColor = outer;
        }
    });
    Main['Color TryApplyingPlayerStringColor(int playerStringColor, Color stringColor)'].hook(
        (original, playerColor, color) => (lineColor ? original(playerColor, lineColor) : original()),
        { whileIn: draw });
}

// ================================ tooltips ================================

// Uma linha do tooltip, como a do tModLoader. `new TooltipLine(Mod, nome,
// texto)` tambem vale (o Mod e ignorado).
class TooltipLine {
    constructor(...args) {
        const [name, text] = args.length >= 3 ? [args[1], args[2]] : args;
        this.Name = String(name);
        this.Text = text === undefined ? '' : String(text);
        this.OverrideColor = undefined;
        this.IsModifier = false;
        this.IsModifierBad = false;
        // true: a linha e o logo da One Drop (o dos ioios do jogo), sem texto.
        this.OneDropLogo = false;
    }
    // '[c/RRGGBB:texto]': a tag de cor que o tooltip entende.
    static colorTag(text, color) { return '[c/' + hexOf(color) + ':' + text + ']'; }
}

function hexOf(color) {
    if (typeof color === 'string') return color.replace(/^#/, '').slice(0, 6).toUpperCase();
    const h = (v) => Math.min(Math.max(Math.round(v || 0), 0), 255).toString(16).padStart(2, '0');
    return (h(color.R) + h(color.G) + h(color.B)).toUpperCase();
}

const COLOR_TAG = /\[c\/([0-9a-fA-F]{6}):([^\]]*)\]/g;
const hasTags = (t) => typeof t === 'string' && t.indexOf('[c/') >= 0;
const stripTags = (t) => t.replace(COLOR_TAG, '$2');

// Texto com tags -> trechos { text, rgb } (rgb ausente = cor da linha).
function colorSegments(text) {
    const out = [];
    let at = 0;
    COLOR_TAG.lastIndex = 0;
    for (let m; (m = COLOR_TAG.exec(text));) {
        if (m.index > at) out.push({ text: text.slice(at, m.index) });
        const v = parseInt(m[1], 16);
        out.push({ text: m[2], rgb: [(v >> 16) & 255, (v >> 8) & 255, v & 255] });
        at = m.index + m[0].length;
    }
    if (at < text.length) out.push({ text: text.slice(at) });
    return out;
}

// O celular monta as linhas no MouseText_DrawItemTooltip_GetLinesInfo, que
// devolve tudo por `ref` (numLines, as linhas especiais), e desenha cada linha
// com um SpriteBatch.DrawString (4 vezes em preto, a sombra, e 1 na cor da
// linha) — o texto sai cru, sem o parser de tags do PC. Entao: as linhas vao
// ao ModifyTooltips; a linha com tag e desenhada por nos, trecho a trecho, e
// a medida dela (a largura da caixa) ignora as tags.
function hookTooltips() {
    const Main = Terraria.Main;
    const G = Microsoft.Xna.Framework.Graphics;
    const drawTooltip = Main['void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)'];
    let drawing = 0;
    drawTooltip.hook((original) => {
        drawing++;
        try { return original(); } finally { drawing--; }
    });

    Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour)'].hook(
        (original, item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor) => {
            original(item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor);
            const m = itemOf(item);
            if (!m || !overrides(m.constructor, ModItem, 'ModifyTooltips')) return;
            const special = new Map([[yoyo.value, 'OneDropLogo'], [research.value, 'JourneyResearch'],
                                     [materials.value, 'Material'], [setBonus.value, 'SetBonus'], [0, 'ItemName']]);
            const list = [];
            for (let i = 0; i < numLines.value; i++) {
                const line = new TooltipLine(special.get(i) || 'Line' + i, lines[i]);
                line.IsModifier = !!pre[i];
                line.IsModifierBad = !!bad[i];
                line.OneDropLogo = i === yoyo.value;
                list.push(line);
            }
            guard(m.constructor.name + '.ModifyTooltips', () => m.ModifyTooltips(item, list));

            // Fora do tooltip (guia de criacao, busca) ninguem desenha as
            // cores: vai o texto limpo.
            const colored = drawing > 0;
            const count = Math.min(list.length, lines.length);
            for (let i = 0; i < count; i++) {
                const line = list[i];
                let text = String(line.Text);
                if (line.OverrideColor) text = TooltipLine.colorTag(stripTags(text), line.OverrideColor);
                lines[i] = colored ? text : stripTags(text);
                pre[i] = !!line.IsModifier;
                bad[i] = !!line.IsModifierBad;
            }
            numLines.value = count;
            const at = (name) => { const i = list.findIndex((l) => l.Name === name); return i < count ? i : -1; };
            // Um logo so, como no tModLoader (o ultimo marcado ganha).
            let logo = -1;
            for (let i = 0; i < count; i++) if (list[i].OneDropLogo) logo = i;
            yoyo.value = logo;
            research.value = at('JourneyResearch');
            materials.value = at('Material');
            setBonus.value = at('SetBonus');
        });

    const measure = 'Vector2 MeasureString(string text)';
    G.SpriteFont[measure].hook((original, font, text) =>
        hasTags(text) ? original(font, stripTags(text)) : original(), { whileIn: drawTooltip });

    G.SpriteBatch['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'].hook(
        (original, batch, font, text, pos, color, rotation, origin, scale, effects, depth) => {
            if (!hasTags(text)) return original();
            // A sombra (preta) fica preta; o texto da linha pega a cor de cada
            // trecho, com o alfa da linha (as cores do jogo sao pre-multiplicadas).
            const shadow = color.R === 0 && color.G === 0 && color.B === 0;
            const k = color.A / 255;
            let x = pos.X;
            for (const seg of colorSegments(text)) {
                if (!seg.text) continue;
                const c = !shadow && seg.rgb ? Color.new(seg.rgb[0] * k, seg.rgb[1] * k, seg.rgb[2] * k, color.A) : color;
                original(batch, font, seg.text, Vector2.new(x, pos.Y), c, rotation, origin, scale, effects, depth);
                x += font[measure](seg.text).X * scale;
            }
            return undefined;
        }, { whileIn: drawTooltip });
}

// Item.Clone (o MemberwiseClone do jogo) copia os campos nativos, mas o
// ModItem mora ao lado: a copia ganha um Clone da instancia do original, com
// o estado dela, como o Item.Clone do tModLoader.
function hookItemClone() {
    Terraria.Item['Item Clone()'].hook((original, self) => {
        const copy = original(self);
        const m = copy ? self.ModItem : undefined;
        if (m && m.Type === self.type) {
            guard(m.constructor.name + '.Clone', () => bindInstance(m.Clone(copy), copy, 'ModItem'));
        }
        return copy;
    }, { minType: FIRST_ITEM, on: -1 });
}

// A arma na mao: o deslocamento gira com o item, como no tModLoader.
function applyHoldout(offset, player) {
    if (!offset || (!offset.X && !offset.Y)) return;
    const x = offset.X * player.direction;
    const y = offset.Y * player.gravDir;
    const r = player.itemRotation;
    const cos = Math.cos(r), sin = Math.sin(r);
    const loc = player.itemLocation;
    loc.X += x * cos - y * sin;
    loc.Y += x * sin + y * cos;
}

// ================================ receitas ================================

let recipesAdded = 0;
let recipesHidden = 0;
const recipeGroupsByName = new Map();

// O jogo (mobile) percorre as receitas ate a posicao 3600, fixo no codigo:
// uma receita alem disso existe, mas o menu de criacao nao a encontra.
const VISIBLE_RECIPES = 3600;

/**
 * Uma receita do jogo, como o ModRecipe do ExMod. So vale no AddRecipes (ou
 * em bl.onContentReady): antes disso o jogo nao montou as receitas dele.
 */
class ModRecipe {
    static MaxIngredients = 15;

    constructor() {
        this.recipe = Terraria.Recipe.currentRecipe;
        this.ingredients = 0;
        this.craftingStation = -1;
        this.customShimmerResults = [];
    }

    static clampStack(it, stack) {
        return Math.max(1, Math.min(stack | 0, it.maxStack || 9999));
    }

    SetResult(type, stack = 1) {
        const it = this.recipe.createItem;
        it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        it.stack = ModRecipe.clampStack(it, stack);
        return this;
    }

    AddIngredient(type, stack = 1) {
        if (this.ingredients >= ModRecipe.MaxIngredients) {
            bl.log('ModRecipe: mais de ' + ModRecipe.MaxIngredients + ' ingredientes; ' + type + ' ficou de fora');
            return this;
        }
        const it = this.recipe.requiredItem[this.ingredients++];
        it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        it.stack = ModRecipe.clampStack(it, stack);
        Terraria.ID.ItemID.Sets.IsAMaterial[type] = true;
        return this;
    }

    /**
     * Aceita qualquer item do grupo no lugar do ingrediente: o grupo (objeto,
     * nome de um do jogo como 'IronBar', ou um criado com CreateRecipeGroup).
     * Se nenhum ingrediente ja posto e do grupo, poe o item-modelo dele com
     * `stack` (como o AddRecipeGroup do tModLoader).
     */
    AddRecipeGroup(group, stack = 1) {
        const g = ModRecipe.GetGroup(group);
        if (!g) {
            bl.log('ModRecipe: grupo de receita "' + group + '" nao existe');
            return this;
        }
        let covered = false;
        for (let i = 0; i < this.ingredients && !covered; i++) {
            covered = g.Contains(this.recipe.requiredItem[i].type);
        }
        if (!covered) this.AddIngredient(g.GetPlaceholderItemType(), stack);
        this.recipe['void RequireGroup(RecipeGroup group)'](g);
        return this;
    }

    // Uma estacao so por receita (o jogo guarda um numero).
    AddTile(tileType) {
        if (this.craftingStation !== -1) {
            bl.log('ModRecipe: a receita ja tem a estacao ' + this.craftingStation + '; ' + tileType + ' ficou de fora');
            return this;
        }
        this.recipe['void SetCraftingStation(int tileType)'](tileType);
        this.craftingStation = tileType;
        return this;
    }

    // O que sai ao jogar o item no Brilho (shimmer), no lugar dos ingredientes.
    AddCustomShimmerResult(type, stack = 1) {
        if (type > 0) {
            this.customShimmerResults.push(
                this.recipe['Item AddCustomShimmerResult(int itemType, int itemStack)'](type, stack));
        }
        return this;
    }

    // needWater, needLava, needHoney, needSnowBiome, needGraveyardBiome,
    // needTorchGodsFavor, needMechdusa, notDecraftable, crimson, corruption,
    // alchemy (sai sozinho com a mesa de alquimia).
    SetProperty(name, value) {
        this.recipe[name] = value;
        return this;
    }

    Register() {
        const Main = Terraria.Main;
        const index = Terraria.Recipe.numRecipes;
        if (index >= Main.recipe.length) {
            // Como o ExMod: a tabela cresce, e a receita existe (decraft,
            // Guia) mesmo que o menu nao chegue nela.
            Main.recipe = Main.recipe.cloneResized(index + 1);
        }
        if (index >= VISIBLE_RECIPES) recipesHidden++;
        Terraria.Recipe['void AddRecipe()']();
        recipesAdded++;
    }

    // O grupo: objeto, nome de um do jogo ('IronBar', 'Wood'...), nome de
    // um criado com CreateRecipeGroup, ou o numero registrado.
    static GetGroup(groupOrName) {
        if (groupOrName && typeof groupOrName === 'object') return groupOrName;
        if (typeof groupOrName === 'number') {
            const all = Terraria.RecipeGroup.recipeGroups;
            return all.ContainsKey(groupOrName) ? all.get_Item(groupOrName) : undefined;
        }
        const mine = recipeGroupsByName.get(groupOrName);
        if (mine) return mine;
        try {
            return Terraria.ID.RecipeGroups[groupOrName] || undefined;
        } catch (e) {
            return undefined;
        }
    }

    static GetGroupByName(name) { return ModRecipe.GetGroup(name); }

    /**
     * Um grupo novo: 'Qualquer <nome>' no menu. O nome vem de
     * Localization (RecipeGroups.<name>) se houver; senao, o proprio `name`.
     * So vale no AddRecipeGroups (ModSystem) ou antes das receitas que o usam.
     */
    static CreateRecipeGroup(name, itemTypes = []) {
        const known = recipeGroupsByName.get(name);
        if (known) return known;
        const key = ModLocalization.Translate('RecipeGroups.' + name);
        const g = Terraria.RecipeGroup.new();
        g['void .ctor(string groupDescriptorKey, int[] validItems)'](key.startsWith('Mods.') ? key : name, itemTypes);
        for (const t of itemTypes) Terraria.ID.ItemID.Sets.IsAMaterial[t] = true;
        g.Register();
        // O item que sai quando uma receita com o grupo e desfeita (Brilho).
        guard('grupo de receita ' + name, () => g['void SortDecraftingEntries()']());
        recipeGroupsByName.set(name, g);
        return g;
    }
}

// Depois de todas: as tabelas que o jogo monta a partir das receitas.
function finishRecipes() {
    if (!recipesAdded) return;
    const steps = [
        () => Terraria.Recipe['void CreateRequiredItemQuickLookups()'](),
        () => Terraria.Recipe['void UpdateMaterialFieldForAllRecipes()'](),
        () => Terraria.Recipe.UpdateWhichItemsAreMaterials(),
        () => Terraria.Recipe.UpdateWhichItemsAreCrafted(),
        () => Terraria.GameContent.ShimmerTransforms.UpdateRecipeSets(),
        () => Terraria.ID.ContentSamples.FixItemsAfterRecipesAreAdded(),
    ];
    for (const s of steps) guard('receitas', s);
    bl.log('receitas de mod: ' + recipesAdded + ' (total ' + Terraria.Recipe.numRecipes + ')');
    if (recipesHidden) {
        bl.log('receitas de mod: ' + recipesHidden + ' alem da posicao ' + VISIBLE_RECIPES +
               ' nao aparecem no menu de criacao (limite do jogo)');
    }
}

// ================================ ModSystem ================================

/**
 * O que e do mod inteiro, nao de um item: grupos de receita e receitas de
 * itens do jogo. Os grupos (de todos os mods) vem antes de qualquer receita.
 */
class ModSystem {
    AddRecipeGroups() {}
    AddRecipes() {}
    PostSetupContent() {}

    static register(cls) {
        const inst = new cls();
        whenReady(() => inst.AddRecipeGroups(), 'groups');
        whenReady(() => {
            inst.AddRecipes();
            inst.PostSetupContent();
        });
        return inst;
    }
}

// ============================= Mod e ModLoader =============================
//
// Conversa entre mods, como no tModLoader: `ModLoader.TryGetMod('outro', ref)`
// acha o Mod de outro pacote pelo `id` do manifesto, e `mod.Call(...)` roda o
// Call que ele definiu. Todos os mods moram no MESMO QuickJS, entao o Call e
// uma chamada de funcao comum: objeto, funcao e classe passam como estao.
//
// Cada mod do registro tem UM objeto Mod, e o registro ja tem todos os mods
// ligados antes de o primeiro main.js rodar (ModLoader.cpp). A classe que o
// mod registra com `Mod.register` vira ESSE objeto — o construtor da base o
// devolve —, entao quem guardou a referencia antes de o mod carregar ve o Call
// dele depois. `bl.mod` e o Mod de quem pergunta.
//
// A ordem de carga e a do uid, aleatoria na pratica: o Call so e garantido do
// PostSetupContent em diante. Antes disso, chamar um mod cujo main.js ainda
// nao rodou lanca um erro que diz isso, em vez de devolver "nada" calado.

const nativeMods = bl.__mods;
const nativeCallerMod = bl.__callerMod;
const nativeDataDirectory = bl.__modDataDirectory;
delete bl.__mods;
delete bl.__callerMod;
delete bl.__modDataDirectory;

const MOD_INFO = Symbol('modInfo');
const modsByUuid = new Map();   // uuid -> Mod, na ordem de carga
let modTableFinal = false;      // nenhum mod pendente: o registro nao muda mais
let adopting = null;            // o objeto que o proximo `new` de um Mod devolve

// Rele o registro nativo enquanto algum mod esta pendente; depois, so o cache.
function syncMods() {
    if (modTableFinal) return;
    let pending = false;
    for (const row of nativeMods()) {
        const mod = modsByUuid.get(row.uuid);
        if (mod) Object.assign(mod[MOD_INFO], row);
        else modsByUuid.set(row.uuid, Object.create(Mod.prototype, { [MOD_INFO]: { value: row } }));
        if (row.state === 'pending') pending = true;
    }
    modTableFinal = !pending && modsByUuid.size > 0;
}

class Mod {
    constructor() {
        const target = adopting;
        adopting = null;
        if (!target) throw new TypeError('Mod: registre a classe com Mod.register(Classe), sem new');
        Object.setPrototypeOf(target, new.target.prototype);
        return target;
    }

    get id() { return this[MOD_INFO].id; }            // o "id" do manifesto (o Name do tModLoader)
    get uuid() { return this[MOD_INFO].uuid; }
    get name() { return this[MOD_INFO].name; }        // o "name" do manifesto, para gente ler
    get version() { return this[MOD_INFO].version; }
    get path() { return this[MOD_INFO].path; }        // a pasta do main.js
    get root() { return this[MOD_INFO].root; }        // a pasta do pacote
    get dataDirectory() { return nativeDataDirectory(this.uuid); }
    toString() { return 'Mod(' + (this.id || this.uuid) + ')'; }

    Load() {}
    AddRecipeGroups() {}
    AddRecipes() {}
    PostSetupContent() {}

    // Quem nao define um Call responde nada, como o `return null` do
    // tModLoader. Mas um mod que ainda nao carregou (ou quebrou) nao pode
    // responder nada calado: quem chamou acharia que ele nao tem o comando.
    Call(...args) {
        syncMods();
        const info = this[MOD_INFO];
        if (info.state === 'pending') {
            throw new Error("Mod '" + (info.id || info.uuid) + "' ainda nao carregou: chame o Call a partir do PostSetupContent");
        }
        if (info.state === 'failed') throw new Error("Mod '" + (info.id || info.uuid) + "' falhou ao carregar");
        return undefined;
    }

    // Um por pacote. Load roda na hora; o resto quando o conteudo esta pronto,
    // como no ModSystem.
    static register(cls) {
        if (typeof cls !== 'function' || !(cls === Mod || cls.prototype instanceof Mod)) {
            throw new TypeError('Mod.register: espera uma classe que estende Mod');
        }
        const mod = bl.mod;
        if (!mod) throw new Error('Mod.register: chamado fora de um mod');
        const info = mod[MOD_INFO];
        if (info.cls) throw new Error("Mod.register: '" + mod.id + "' ja registrou " + info.cls.name + ' (um Mod por pacote)');
        adopting = mod;
        try {
            new cls();
        } catch (e) {
            Object.setPrototypeOf(mod, Mod.prototype);
            throw e;
        } finally {
            adopting = null;
        }
        info.cls = cls;
        const name = cls.name;
        guard(name + '.Load', () => mod.Load());
        whenReady(() => guard(name + '.AddRecipeGroups', () => mod.AddRecipeGroups()), 'groups');
        whenReady(() => {
            guard(name + '.AddRecipes', () => mod.AddRecipes());
            guard(name + '.PostSetupContent', () => mod.PostSetupContent());
        });
        return mod;
    }
}

// Pelo id do manifesto ou pelo uuid. Mod que falhou ao carregar nao conta. Dois
// mods com o mesmo id (o id e so um apelido, o site nao garante que e unico):
// nenhum, e o log manda usar o uuid.
function findMod(name, api) {
    if (typeof name !== 'string' || !name) throw new TypeError(api + ': espera o id do mod (texto)');
    syncMods();
    let found = null;
    let count = 0;
    for (const mod of modsByUuid.values()) {
        const info = mod[MOD_INFO];
        if (info.state === 'failed') continue;
        if (info.uuid === name) return mod;
        if (info.id === name) { found = mod; count++; }
    }
    if (count < 2) return found;
    if (!reported.has('ModLoader:' + name)) {
        reported.add('ModLoader:' + name);
        bl.log("ModLoader: " + count + " mods com o id '" + name + "'; peca pelo uuid");
    }
    return null;
}

const ModLoader = Object.freeze({
    // Como o `TryGetMod(string, out Mod)` do tModLoader: o Mod vai no Ref.
    TryGetMod(name, result) {
        const mod = findMod(name, 'ModLoader.TryGetMod');
        if (result !== undefined) {
            if (result === null || typeof result !== 'object') {
                throw new TypeError('ModLoader.TryGetMod(id, ref): o segundo argumento e um Ref (new Ref())');
            }
            result.value = mod;
        }
        return mod !== null;
    },
    // Para dependencia obrigatoria: lanca se o mod nao esta.
    GetMod(name) {
        const mod = findMod(name, 'ModLoader.GetMod');
        if (!mod) throw new Error("ModLoader.GetMod: nenhum mod '" + name + "' carregado (se ele e opcional, use TryGetMod)");
        return mod;
    },
    HasMod(name) {
        return findMod(name, 'ModLoader.HasMod') !== null;
    },
    get Mods() {
        syncMods();
        return [...modsByUuid.values()].filter((m) => m[MOD_INFO].state !== 'failed');
    },
});

Object.defineProperty(bl, 'mod', {
    get() {
        const uuid = nativeCallerMod();
        if (!uuid) return undefined;
        syncMods();
        return modsByUuid.get(uuid);
    },
    configurable: true,
    enumerable: true,
});

// ================================ ModPlayer ================================
//
// Como no tModLoader: cada JOGADOR tem a propria instancia de cada ModPlayer
// registrado, criada na primeira vez que alguem pergunta por ela. Mora ao lado
// do Player do jogo (campo extra `ModPlayers`); `this.Player` e o jogador dela.
// No multijogador cada jogador da tela tem a sua: o escudo de um nao liga o
// dash do outro.
//
// Os metodos recebem o jogador como primeiro argumento (como no ExMod), que e
// o mesmo `this.Player`.

const modPlayerClasses = [];
// Classe -> "<uid do mod>/<Classe>": a chave dos dados salvos dela.
const modPlayerKeys = new Map();

function modPlayersOf(player) {
    let all = player.ModPlayers;
    if (all === undefined || all.__count !== modPlayerClasses.length) {
        const fresh = all === undefined;
        if (fresh) {
            all = Object.create(null);
            Object.defineProperty(all, '__count', { value: 0, writable: true });
        }
        const addr = bl.addressOf(player);
        const created = [];
        for (const cls of modPlayerClasses) {
            if (all[cls.name]) continue;
            const inst = new cls();
            inst.__entity = addr;
            all[cls.name] = inst;
            created.push(inst);
        }
        all.__count = modPlayerClasses.length;
        if (fresh) player.ModPlayers = all;
        for (const inst of created) guard(inst.constructor.name + '.Initialize', () => inst.Initialize());
    }
    return all;
}

// As classes registradas que escreveram `method` (as outras nem sao chamadas).
const modPlayerOverriders = new Map();
function overridersOf(method) {
    let list = modPlayerOverriders.get(method);
    if (!list) {
        list = modPlayerClasses.filter((c) => overrides(c, ModPlayer, method));
        modPlayerOverriders.set(method, list);
    }
    return list;
}

// fn(inst) para cada ModPlayer do jogador que escreveu `method`.
function eachModPlayer(player, method, fn) {
    const list = overridersOf(method);
    if (list.length === 0) return;
    const all = modPlayersOf(player);
    for (const cls of list) {
        const inst = all[cls.name];
        guard(cls.name + '.' + method, () => fn(inst));
    }
}

// Algum ModPlayer do jogador devolveu `value`?
function anyModPlayer(player, method, value, fn) {
    let hit = false;
    eachModPlayer(player, method, (inst) => { if (fn(inst) === value) hit = true; });
    return hit;
}

class ModPlayer {
    // O Player do jogo desta instancia.
    get Player() { return entityOf(this); }

    // ExMod: somados ao maximo de vida/mana no ModifyMaxStats.
    CumulativeHealth = 0;
    CumulativeMana = 0;
    WeaponDamage = 0;

    // Uma vez, quando a instancia nasce (o jogador pergunta por ela).
    Initialize() {}
    // O jogador entrou no mundo (o seu e os outros, no multijogador).
    OnEnterWorld(player) {}
    // Voltou a viver depois de morrer.
    OnRespawn(player) {}

    // Todo quadro, antes de tudo: zere aqui o que os acessorios ligam.
    ResetEffects(player) {}
    // Depois do ResetEffects: this.CumulativeHealth/Mana somam ao maximo.
    ModifyMaxStats(player) {
        this.CumulativeHealth = 0;
        this.CumulativeMana = 0;
    }
    PreUpdate(player) {}
    PostUpdate(player) {}
    PreUpdateBuffs(player) {}
    PostUpdateBuffs(player) {}
    // Depois dos equipamentos e acessorios (o PostUpdateEquips do tModLoader).
    UpdateEquips(player) {}
    PostUpdateEquips(player) {}
    // Antes (Bad) e depois da regeneracao de vida do jogo; de mana, depois.
    UpdateBadLifeRegen(player) {}
    UpdateLifeRegen(player) {}
    UpdateManaRegen(player) {}
    // Todo quadro morto.
    UpdateDead(player) {}
    // Movimento proprio (dash...): no fim do quadro, antes das bordas do mundo.
    UpdateMovement(player) {}

    // false impede usar o item.
    CanUseItem(player, item) { return true; }
    // Dano da arma: devolva o novo, ou ponha em this.WeaponDamage.
    ModifyWeaponDamage(player, item, damage) { this.WeaponDamage = damage; }

    // true: o golpe nao acontece (nem tira vida, nem da imunidade).
    ImmuneTo(player, damageSource, cooldownCounter, dodgeable) { return false; }
    // true: esquiva (como o Cinto Negro).
    FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) { return false; }
    // modifiers = { damage, hitDirection, quiet, crit, dodgeable }: mude o que quiser.
    ModifyHurt(player, modifiers) {}
    // Depois de tomar dano (damage = o que tirou de verdade).
    OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    // Idem, so se continuou vivo.
    PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable) {}
    // false impede a morte (ponha player.statLife > 0, senao ele segue com 0).
    PreKill(player, damageSource, damage, hitDirection, pvp) { return true; }
    Kill(player, damageSource, damage, hitDirection, pvp) {}

    // Dados do jogador que ficam no personagem: ponha em `data` (objeto JS
    // comum, vira JSON) e leia de volta no LoadData. Chamado a cada save.
    SaveData(data) {}
    // Ao carregar o personagem, com o que o SaveData gravou (so se gravou).
    LoadData(data) {}

    // A instancia desta classe no jogador: ExampleDashPlayer.get(player).
    static get(player) { return modPlayersOf(player)[this.name]; }

    // ExMod: a instancia do jogador LOCAL (a da tela deste aparelho).
    static getByName(name) {
        return modPlayersOf(Terraria.Main.player[Terraria.Main.myPlayer])[name];
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModPlayer)) {
            throw new TypeError('ModPlayer.register(Classe): passe a classe, que estende ModPlayer');
        }
        if (modPlayerClasses.some((c) => c.name === cls.name)) {
            throw new TypeError('ModPlayer.register: ja existe um ModPlayer chamado ' + cls.name);
        }
        once('player.fields', () => {
            defineEntityField(Terraria.Player, 'ModPlayers');
            // player.GetModPlayer(Classe) ou player.GetModPlayer('Nome').
            bl.defineMethod(Terraria.Player, 'GetModPlayer', function (which) {
                const name = typeof which === 'string' ? which : which && which.name;
                return modPlayersOf(this)[name];
            });
        });
        modPlayerClasses.push(cls);
        modPlayerKeys.set(cls.name, (bl.mod ? bl.mod.uuid : 'sem-mod') + '/' + cls.name);
        modPlayerOverriders.clear();
        hookModPlayer(cls);
        if (overrides(cls, ModPlayer, 'SaveData') || overrides(cls, ModPlayer, 'LoadData')) {
            once('player.save', installModPlayerSave);
        }
        return cls;
    }
}

// ------------------------- dados salvos do ModPlayer -------------------------
//
// `<personagem>.plr.bl.json`, ao lado do save do jogo: { "<uid>/<Classe>": data }.
// Dados de um mod que nao esta carregado agora continuam no arquivo (o save
// le o que havia e so troca as chaves que conhece).

function modPlayerDataFile(fileData) {
    if (!fileData || fileData.IsCloudSave) return null;
    const path = fileData.Path;
    return path ? path + '.bl.json' : null;
}

function readModPlayerData(file) {
    const txt = bl.file.read(file);
    if (!txt) return {};
    try {
        return JSON.parse(txt) || {};
    } catch (e) {
        bl.log('ModPlayer: ' + file + ' esta quebrado (' + e + '); os dados salvos foram ignorados');
        return {};
    }
}

function installModPlayerSave() {
    const P = Terraria.Player;
    P['void InternalSavePlayerFile(PlayerFileData playerFile)'].hook((original, fileData) => {
        original(fileData);
        const file = modPlayerDataFile(fileData);
        const player = file ? fileData.Player : null;
        if (!player) return;
        const all = readModPlayerData(file);
        const mine = modPlayersOf(player);
        for (const cls of modPlayerClasses) {
            if (!overrides(cls, ModPlayer, 'SaveData')) continue;
            const key = modPlayerKeys.get(cls.name);
            const data = {};
            guard(cls.name + '.SaveData', () => mine[cls.name].SaveData(data));
            if (Object.keys(data).length) all[key] = data;
            else delete all[key];
        }
        guard('ModPlayer: gravar ' + file, () => {
            if (Object.keys(all).length) bl.file.write(file, JSON.stringify(all));
            else bl.file.delete(file);
        });
    });
    P['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'].hook((original, path, cloud) => {
        const fileData = original(path, cloud);
        const file = modPlayerDataFile(fileData);
        const player = file ? fileData.Player : null;
        if (!player) return fileData;
        const all = readModPlayerData(file);
        const mine = modPlayersOf(player);
        for (const cls of modPlayerClasses) {
            const data = all[modPlayerKeys.get(cls.name)];
            if (data !== undefined) guard(cls.name + '.LoadData', () => mine[cls.name].LoadData(data));
        }
        return fileData;
    });
}

function hookModPlayer(cls) {
    const P = Terraria.Player;
    const has = (name) => overrides(cls, ModPlayer, name);

    if (has('ResetEffects') || has('ModifyMaxStats')) once('player.ResetEffects', () => {
        P['void ResetEffects()'].hook((original, self) => {
            original(self);
            eachModPlayer(self, 'ResetEffects', (m) => m.ResetEffects(self));
            let life = 0, mana = 0;
            eachModPlayer(self, 'ModifyMaxStats', (m) => {
                m.ModifyMaxStats(self);
                life += m.CumulativeHealth || 0;
                mana += m.CumulativeMana || 0;
            });
            if (life) self.statLifeMax2 = Math.max(1, self.statLifeMax2 + life);
            if (mana) self.statManaMax2 = Math.max(0, self.statManaMax2 + mana);
        });
    });

    if (has('PreUpdate') || has('PostUpdate')) once('player.Update', () => {
        P['void Update(int i)'].hook((original, self, i) => {
            eachModPlayer(self, 'PreUpdate', (m) => m.PreUpdate(self));
            original(self, i);
            eachModPlayer(self, 'PostUpdate', (m) => m.PostUpdate(self));
        });
    });

    if (has('PreUpdateBuffs') || has('PostUpdateBuffs')) once('player.UpdateBuffs', () => {
        P['void UpdateBuffs(int i)'].hook((original, self, i) => {
            eachModPlayer(self, 'PreUpdateBuffs', (m) => m.PreUpdateBuffs(self));
            original(self, i);
            eachModPlayer(self, 'PostUpdateBuffs', (m) => m.PostUpdateBuffs(self));
        });
    });

    if (has('UpdateEquips') || has('PostUpdateEquips')) once('player.UpdateEquips', () => {
        P['void UpdateEquips(int i)'].hook((original, self, i) => {
            original(self, i);
            eachModPlayer(self, 'UpdateEquips', (m) => m.UpdateEquips(self));
            eachModPlayer(self, 'PostUpdateEquips', (m) => m.PostUpdateEquips(self));
        });
    });

    if (has('UpdateBadLifeRegen') || has('UpdateLifeRegen')) once('player.LifeRegen', () => {
        P['void UpdateLifeRegen()'].hook((original, self) => {
            eachModPlayer(self, 'UpdateBadLifeRegen', (m) => m.UpdateBadLifeRegen(self));
            original(self);
            eachModPlayer(self, 'UpdateLifeRegen', (m) => m.UpdateLifeRegen(self));
        });
    });

    if (has('UpdateManaRegen')) once('player.ManaRegen', () => {
        P['void UpdateManaRegen()'].hook((original, self) => {
            original(self);
            eachModPlayer(self, 'UpdateManaRegen', (m) => m.UpdateManaRegen(self));
        });
    });

    if (has('UpdateDead')) once('player.UpdateDead', () => {
        P['void UpdateDead()'].hook((original, self) => {
            original(self);
            eachModPlayer(self, 'UpdateDead', (m) => m.UpdateDead(self));
        });
    });

    if (has('UpdateMovement')) once('player.Movement', () => {
        P['void BordersMovement()'].hook((original, self) => {
            eachModPlayer(self, 'UpdateMovement', (m) => m.UpdateMovement(self));
            original(self);
        });
    });

    if (has('OnEnterWorld')) once('player.EnterWorld', () => {
        P.Hooks['void EnterWorld(int playerIndex)'].hook((original, index) => {
            original(index);
            const player = Terraria.Main.player[index];
            eachModPlayer(player, 'OnEnterWorld', (m) => m.OnEnterWorld(player));
        });
    });

    if (has('OnRespawn')) once('player.Spawn', () => {
        const REVIVE = Terraria.PlayerSpawnContext.ReviveFromDeath;
        P['void Spawn(PlayerSpawnContext context)'].hook((original, self, context) => {
            original(self, context);
            if (context === REVIVE) eachModPlayer(self, 'OnRespawn', (m) => m.OnRespawn(self));
        });
    });

    if (has('CanUseItem')) once('player.CanUseItem', () => {
        P['bool ItemCheck_CheckCanUse_Inner(Item sItem, bool ignoreCursed)'].hook((original, self, item, ignoreCursed) => {
            if (anyModPlayer(self, 'CanUseItem', false, (m) => m.CanUseItem(self, item))) return false;
            return original(self, item, ignoreCursed);
        });
    });

    if (has('ModifyWeaponDamage')) once('player.WeaponDamage', () => {
        P['int GetWeaponDamage(Item sItem)'].hook((original, self, item) => {
            let damage = original(self, item);
            eachModPlayer(self, 'ModifyWeaponDamage', (m) => {
                m.WeaponDamage = damage;
                const r = m.ModifyWeaponDamage(self, item, damage);
                damage = typeof r === 'number' ? r : (typeof m.WeaponDamage === 'number' ? m.WeaponDamage : damage);
            });
            return Math.floor(damage);
        });
    });

    const hurt = ['ImmuneTo', 'FreeDodge', 'ModifyHurt', 'OnHurt', 'PostHurt'];
    if (hurt.some(has)) once('player.Hurt', () => {
        P['double Hurt(PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet, bool Crit, int cooldownCounter, bool dodgeable)'].hook(
            (original, self, src, damage, dir, pvp, quiet, crit, cooldown, dodgeable) => {
                if (anyModPlayer(self, 'ImmuneTo', true, (m) => m.ImmuneTo(self, src, cooldown, dodgeable))) return 0;
                if (anyModPlayer(self, 'FreeDodge', true,
                    (m) => m.FreeDodge(self, src, damage, dir, pvp, quiet, crit, cooldown, dodgeable))) return 0;
                const mod = { damage, hitDirection: dir, quiet, crit, dodgeable };
                eachModPlayer(self, 'ModifyHurt', (m) => m.ModifyHurt(self, mod));
                const done = original(self, src, Math.floor(mod.damage), mod.hitDirection, pvp,
                                      mod.quiet, mod.crit, cooldown, mod.dodgeable);
                if (done > 0) {
                    eachModPlayer(self, 'OnHurt',
                        (m) => m.OnHurt(self, src, done, mod.hitDirection, pvp, mod.quiet, mod.crit, cooldown, mod.dodgeable));
                    if (!self.dead && self.statLife > 0) {
                        eachModPlayer(self, 'PostHurt',
                            (m) => m.PostHurt(self, src, done, mod.hitDirection, pvp, mod.quiet, mod.crit, cooldown, mod.dodgeable));
                    }
                }
                return done;
            });
    });

    if (has('PreKill') || has('Kill')) once('player.KillMe', () => {
        P['void KillMe(PlayerDeathReason damageSource, double dmg, int hitDirection, bool pvp)'].hook(
            (original, self, src, dmg, dir, pvp) => {
                const dies = !self.dead && !self.creativeGodMode;
                if (dies && anyModPlayer(self, 'PreKill', false, (m) => m.PreKill(self, src, dmg, dir, pvp))) return;
                original(self, src, dmg, dir, pvp);
                if (dies && self.dead) eachModPlayer(self, 'Kill', (m) => m.Kill(self, src, dmg, dir, pvp));
            });
    });
}

// ================================= ModBuff =================================

const buffsByType = new Map();

// { cultura: texto } passado por `modify` (ModifyDisplayName/ModifyDescription
// do mod, que mexe no campo como no ExMod), cultura por cultura.
function modifyPerCulture(inst, field, texts, modify) {
    if (!texts || typeof texts !== 'object') {
        inst[field] = texts || '';
        guard(inst.constructor.name + '.' + modify, () => inst[modify]());
        return inst[field];
    }
    const out = {};
    for (const c of Object.keys(texts)) {
        inst[field] = texts[c];
        guard(inst.constructor.name + '.' + modify, () => inst[modify]());
        out[c] = inst[field];
    }
    return out;
}

/**
 * Um buff novo, como o ModBuff do ExMod. Uma instancia por tipo (buff nao e
 * entidade): os metodos recebem o jogador/NPC e o indice na lista dele.
 */
class ModBuff {
    Type = undefined;
    // true: fora do Mod Menu.
    HideFromModMenu = false;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: BuffName.<Classe> e
    // BuffDescription.<Classe> em Localization/*.json.
    DisplayName = '';
    Description = '';
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;

    // Uma vez, com o tipo ja nas tabelas: Main.debuff[this.Type] = true...
    SetStaticDefaults() {}
    PostStaticDefaults() {}
    PostSetupContent() {}
    // Mexa em this.DisplayName / this.Description (uma vez por idioma).
    ModifyDisplayName() {}
    ModifyDescription() {}

    // Todo quadro, com o buff ativo.
    UpdatePlayer(player, buffIndex) {}
    UpdateNPC(npc, buffIndex) {}
    // Quando o buff entra (nao estava ativo).
    ApplyPlayer(player, buffTime) {}
    ApplyNPC(npc, buffTime) {}
    // Quando entra de novo, ja ativo; false impede o jogo de renovar o tempo.
    ReApplyPlayer(player, buffTime, buffIndex) { return true; }
    ReApplyNPC(npc, buffTime, buffIndex) { return true; }
    // Tocar no icone para tirar: true/false decide; null = o do jogo (debuff nao sai).
    CanRemove(player, buffTime, buffIndex, debuff) { return null; }
    OnRemove(player, buffTime, buffIndex) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModBuff)) {
            throw new TypeError('ModBuff.register(Classe): passe a classe, que estende ModBuff');
        }
        const inst = new cls();
        adoptTemplate(cls, inst);
        const name = cls.name;
        const displayName = modifyPerCulture(inst, 'DisplayName',
            inst.DisplayName || localized('BuffName', name) || name, 'ModifyDisplayName');
        const description = modifyPerCulture(inst, 'Description',
            inst.Description || localized('BuffDescription', name) || '', 'ModifyDescription');
        const type = bl.buffs.register({
            name,
            texture: texturePath(inst.Texture || name),
            displayName,
            description,
            setStaticDefaults() {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                applyMenuVisibility('buff', inst, inst.Type, () => false);
            },
        });
        inst.Type = type;
        buffsByType.set(type, inst);
        whenReady(() => inst.PostSetupContent());
        hookBuff(cls);
        return type;
    }

    static isModType(type) { return bl.buffs.isModBuff(type); }
    // O tipo de um buff DESTE mod pelo nome da classe; -1 se nao ha.
    static getTypeByName(name) { return bl.buffs.typeOf(name); }
    static getModBuff(type) { return buffsByType.get(type); }
}

// Os buffs de mod ativos de `entity` (jogador ou NPC): fn(m, i).
function forEachModBuff(entity, fn) {
    const types = entity.buffType;
    const times = entity.buffTime;
    for (let i = 0; i < types.length; i++) {
        const t = types[i];
        if (t < FIRST_BUFF || times[i] <= 0) continue;
        const m = buffsByType.get(t);
        if (!m) continue;
        fn(m, i);
        // O fn tirou o buff (DelBuff): o jogo puxa os de tras uma posicao, e
        // o que veio para i ainda nao rodou (o buffIndex-- do tModLoader).
        if (types[i] !== t) i--;
    }
}

function hookBuff(cls) {
    const P = Terraria.Player;
    const has = (name) => overrides(cls, ModBuff, name);

    // Dentro do UpdateBuffs: depois do ResetEffects e antes dos acessorios,
    // como os buffs do jogo (defesa somada aqui vale no quadro).
    if (has('UpdatePlayer')) once('buff.UpdatePlayer', () => {
        P['void UpdateBuffs(int i)'].hook((original, self, i) => {
            original(self, i);
            forEachModBuff(self, (m, idx) =>
                guard(m.constructor.name + '.UpdatePlayer', () => m.UpdatePlayer(self, idx)));
        });
    });

    if (has('UpdateNPC')) once('buff.UpdateNPC', () => {
        Terraria.NPC['void UpdateNPC_BuffSetFlags(bool lowerBuffTime)'].hook((original, self, lower) => {
            original(self, lower);
            forEachModBuff(self, (m, idx) =>
                guard(m.constructor.name + '.UpdateNPC', () => m.UpdateNPC(self, idx)));
        });
    });

    if (has('ApplyPlayer')) once('buff.ApplyPlayer', () => {
        P['bool AddBuff_ActuallyTryToAddTheBuff(int type, int time)'].hook((original, self, type, time) => {
            const ok = original(self, type, time);
            const m = ok ? buffsByType.get(type) : undefined;
            if (m) guard(m.constructor.name + '.ApplyPlayer', () => m.ApplyPlayer(self, time));
            return ok;
        });
    });

    if (has('ReApplyPlayer')) once('buff.ReApplyPlayer', () => {
        P['bool AddBuff_TryUpdatingExistingBuffTime(int type, int time)'].hook((original, self, type, time) => {
            const m = buffsByType.get(type);
            if (m) {
                const idx = self['int FindBuffIndex(int type)'](type);
                if (idx >= 0 && guard(m.constructor.name + '.ReApplyPlayer',
                    () => m.ReApplyPlayer(self, time, idx)) === false) {
                    return true;   // "ja estava": o jogo nao poe outro
                }
            }
            return original(self, type, time);
        });
    });

    if (has('ApplyNPC') || has('ReApplyNPC')) once('buff.NPC', () => {
        Terraria.NPC['void AddBuff(int type, int time, bool quiet)'].hook((original, self, type, time, quiet) => {
            const m = buffsByType.get(type);
            if (!m) return original(self, type, time, quiet);
            const idx = self['int FindBuffIndex(int type)'](type);
            if (idx >= 0) {
                if (guard(m.constructor.name + '.ReApplyNPC', () => m.ReApplyNPC(self, time, idx)) === false) return;
                return original(self, type, time, quiet);
            }
            original(self, type, time, quiet);
            if (self['int FindBuffIndex(int type)'](type) >= 0) {
                guard(m.constructor.name + '.ApplyNPC', () => m.ApplyNPC(self, time));
            }
        });
    });

    // O toque no icone da barra. Com o jogador; o indice e o da lista dele.
    if (has('CanRemove') || has('OnRemove')) once('buff.Remove', () => {
        bl.classOf('', 'GUIBuffs')['void RemoveBuff(int buff)'].hook((original, self, idx) => {
            const player = Terraria.Main.player[Terraria.Main.myPlayer];
            const type = player.buffType[idx];
            const time = player.buffTime[idx];
            const m = buffsByType.get(type);
            if (!m) return original(self, idx);
            const can = guard(m.constructor.name + '.CanRemove',
                () => m.CanRemove(player, time, idx, !!Terraria.Main.debuff[type]));
            if (can === false) return;
            if (can === true && Terraria.Main.debuff[type]) {
                player['void DelBuff(int b)'](idx);
            } else {
                original(self, idx);
            }
            if (player.buffType[idx] !== type) {
                guard(m.constructor.name + '.OnRemove', () => m.OnRemove(player, time, idx));
            }
        });
    });
}

// ================================ ModTile ================================

const tilesByType = new Map();

/**
 * Um tile novo (bloco), como o ModTile do tModLoader. Uma instancia por tipo:
 * os metodos recebem a posicao (i, j) em tiles.
 */
class ModTile {
    Type = undefined;
    // Relativo a Textures/, sem .png: a folha de quadros, como as do jogo.
    Texture = this.constructor.name;
    // A poeira ao bater e quebrar (Terraria.ID.DustID).
    DustType = 0;
    // O som ao bater: um SoundID (numero ou estilo). undefined = o do jogo.
    HitSound = undefined;
    // Picareta minima para quebrar, e quanto o tile resiste (2 = o dobro de golpes).
    MinPick = 0;
    MineResist = 1;
    // O item que cai. undefined = o item de mod que coloca este tile.
    ItemDrop = undefined;

    // Uma vez, com o tipo ja nas tabelas: Main.tileSolid[this.Type] = true...
    SetStaticDefaults() {}
    PostSetupContent() {}
    // A cor no mapa: a do jogo mais proxima (o .map so leva cor que o jogo
    // conhece). Sem AddMapEntry, o tile fica fora do mapa.
    AddMapEntry(color, name) {
        (this.mapEntries || (this.mapEntries = [])).push({ color, name });
        if (this.mapEntries.length === 1) bl.tiles.setMapColor(this.Type, color.R, color.G, color.B);
    }

    // false: a picareta nao quebra.
    CanKillTile(i, j) { return true; }
    // Antes de o tile sair (fail: so o golpe, sem quebrar).
    KillTile(i, j, fail, effectOnly, noItem) {}
    // false: sem poeira; false no KillSound: sem som.
    CreateDust(i, j) { return true; }
    KillSound(i, j, fail) { return true; }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModTile)) {
            throw new TypeError('ModTile.register(Classe): passe a classe, que estende ModTile');
        }
        const inst = new cls();
        adoptTemplate(cls, inst);
        const name = cls.name;
        const type = bl.tiles.register({
            name,
            texture: texturePath(inst.Texture || name),
            setStaticDefaults() {
                inst.SetStaticDefaults();
            },
        });
        inst.Type = type;
        tilesByType.set(type, inst);
        whenReady(() => guard(name + '.PostSetupContent', () => inst.PostSetupContent()));
        hookTiles();
        return type;
    }

    static isModType(type) { return bl.tiles.isModTile(type); }
    // O tipo de um tile DESTE mod pelo nome da classe; -1 se nao ha.
    static getTypeByName(name) { return bl.tiles.typeOf(name); }
    static getModTile(type) { return tilesByType.get(type); }
}

// O item que cai do tile: o ItemDrop, ou o item de mod que o coloca.
let tileItems = null;
function tileItemDrop(m) {
    if (m.ItemDrop !== undefined) return m.ItemDrop;
    if (!tileItems) {
        tileItems = new Map();
        const it = Terraria.Item.new();
        it['void .ctor()']();
        for (const t of itemsByType.keys()) {
            it['void SetDefaults(int Type, ItemVariant variant)'](t, null);
            if (it.createTile >= FIRST_TILE && !tileItems.has(it.createTile)) tileItems.set(it.createTile, t);
        }
    }
    return tileItems.get(m.Type) || 0;
}

// Os hooks do jogo, uma vez. O filtro nativo pelo tipo do tile (tile/tileAt)
// deixa os tiles do jogo fora do JS: bater em terra nao paga nada.
function hookTiles() {
    once('tile.hooks', () => {
        const W = Terraria.WorldGen;
        const at = (i, j) => tilesByType.get(bl.tiles.typeAt(i, j));

        Terraria.Player['int GetPickaxeDamage(int x, int y, int pickPower, int hitBufferIndex, Tile tileTarget)'].hook(
            (original, self, x, y, pickPower, hit, tile) => {
                const damage = original(self, x, y, pickPower, hit, tile);
                const m = at(x, y);
                if (!m) return damage;
                if (pickPower < m.MinPick) return 0;
                return m.MineResist > 0 ? Math.floor(damage / m.MineResist) : damage;
            }, { minType: FIRST_TILE, tile: 4 });

        W['bool CanKillTile(int i, int j, out bool blockDamaged)'].hook((original, i, j, blockDamaged) => {
            const m = at(i, j);
            if (m && guard(m.constructor.name + '.CanKillTile', () => m.CanKillTile(i, j)) === false) {
                blockDamaged.value = false;
                return false;
            }
            return original(i, j, blockDamaged);
        }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'].hook(
            (original, i, j, fail, effectOnly, noItem) => {
                const m = at(i, j);
                if (m) guard(m.constructor.name + '.KillTile', () => m.KillTile(i, j, fail, effectOnly, noItem));
                return original(i, j, fail, effectOnly, noItem);
            }, { minType: FIRST_TILE, tileAt: [0, 1] });

        W['void KillTile_GetItemDrops(int x, int y, Tile tileCache, out int dropItem, out int dropItemStack, out int secondaryItem, out int secondaryItemStack, out bool noPrefix, bool includeLargeObjectDrops)'].hook(
            (original, x, y, tile, drop, stack, second, secondStack, noPrefix, large) => {
                original(x, y, tile, drop, stack, second, secondStack, noPrefix, large);
                const m = at(x, y);
                const item = m ? tileItemDrop(m) : 0;
                if (item > 0) {
                    drop.value = item;
                    stack.value = 1;
                }
            }, { minType: FIRST_TILE, tile: 2 });

        const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
        W['int KillTile_MakeTileDust(int i, int j, Tile tileCache)'].hook((original, i, j, tile) => {
            const m = at(i, j);
            if (!m) return original(i, j, tile);
            if (guard(m.constructor.name + '.CreateDust', () => m.CreateDust(i, j)) === false) return 6000;
            return newDust(Vector2.new(i * 16, j * 16), 16, 16, m.DustType, 0, 0, 0, Color.White, 1);
        }, { minType: FIRST_TILE, tile: 2 });

        const playInt = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'];
        const playStyle = Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'];
        W['void KillTile_PlaySounds(int i, int j, bool fail, Tile tileCache)'].hook((original, i, j, fail, tile) => {
            const m = at(i, j);
            if (!m) return original(i, j, fail, tile);
            if (guard(m.constructor.name + '.KillSound', () => m.KillSound(i, j, fail)) === false) return undefined;
            const s = m.HitSound;
            if (s === undefined || s === null) return original(i, j, fail, tile);
            if (typeof s === 'number') playInt(s, i * 16, j * 16, 1, 1, 0);
            else playStyle(s, Vector2.new(i * 16, j * 16), 0, 1);
            return undefined;
        }, { minType: FIRST_TILE, tile: 3 });
    });
}

// ============================== ModProjectile ==============================

const projectilesByType = new Map();

// Os campos que o CloneDefaults copia (os do ProjectileLoader do ExMod, mais
// tamanho).
const CLONED_PROJECTILE_FIELDS = [
    'width', 'height', 'ownerHitCheckDistance', 'counterweight', 'sentry', 'arrow', 'bobber',
    'numHits', 'netImportant', 'manualDirectionChange', 'decidesManualFallThrough',
    'shouldFallThrough', 'bannerIdToRespondTo', 'stopsDealingDamageAfterPenetrateHits',
    'localNPCHitCooldown', 'idStaticNPCHitCooldown', 'usesLocalNPCImmunity',
    'usesIDStaticNPCImmunity', 'usesOwnerMeleeHitCD', 'appliesImmunityTimeOnSingleHits',
    'noDropItem', 'minion', 'minionSlots', 'soundDelay', 'spriteDirection', 'melee', 'ranged',
    'magic', 'ownerHitCheck', 'drawLayer', 'usesOwnerLight', 'hide', 'ignoreWater', 'hostile',
    'reflected', 'extraUpdates', 'light', 'penetrate', 'tileCollide', 'aiStyle', 'alpha',
    'rotation', 'scale', 'timeLeft', 'friendly', 'damage', 'originalDamage', 'knockBack',
    'coldDamage', 'noEnchantments', 'noEnchantmentVisuals', 'trap', 'npcProj',
    'tagEffectType', 'bonusTagDamage', 'armorPenetration', 'bonusCritChance',
];
const projectileOf = (p) => instanceOf(p, 'ModProjectile', projectilesByType);

class ModProjectile {
    // O Projectile do jogo desta instancia (no molde, undefined).
    get Projectile() { return entityOf(this); }
    // A instancia de uma entidade nova, a partir desta (o molde, ou a de um
    // item que o jogo copiou). Sobrescreva para copiar fundo o que for seu.
    Clone(newProjectile) { return cloneInstance(this); }
    Type = undefined;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: ProjectileName.<Classe>
    // em Localization/*.json, e sem isso o nome da classe.
    DisplayName = '';
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;

    // Uma vez, com o tipo ja no jogo. Main.projFrames[this.Type] escrito aqui
    // vale como a quantidade de quadros da textura.
    SetStaticDefaults() {}
    SetDefaults(proj) {}
    PostStaticDefaults() {}
    PostSetDefaults(proj) {}
    PostSetupContent() {}

    // Usa a IA de outro projetil do jogo: o tipo e trocado so durante a IA
    // do jogo (o aiStyle sozinho nao basta para mangual, gancho...).
    AIType = 0;

    // Uma vez, no primeiro quadro de vida do projetil.
    OnSpawn(proj) {}
    // IA: false no PreAI pula a IA do jogo (a do aiStyle) e o AI.
    PreAI(proj) { return true; }
    AI(proj) {}
    PostAI(proj) {}
    // Morte: false no PreKill tira os efeitos do jogo (poeira, som); morre igual.
    PreKill(proj, timeLeft) { return true; }
    OnKill(proj, timeLeft) {}
    // Bateu num bloco (tileCollide) e o jogo ia mata-lo: false o mantem vivo
    // (para quicar, mude proj.velocity aqui). oldVelocity e a de antes do choque.
    OnTileCollide(proj, oldVelocity) { return true; }
    // Acertou um NPC / um jogador.
    OnHitNPC(proj, npc) {}
    OnHitPlayer(proj, player) {}
    // true/false decide o acerto contra targetRect; undefined = o do jogo.
    Colliding(proj, projHitbox, targetHitbox) { return undefined; }
    // false: nao causa dano (nem chama o Damage do jogo).
    CanDamage(proj) { return true; }
    // Pet e lacaio (Main.projPet) nao ferem ao encostar; true libera o dano
    // de contato do lacaio.
    MinionContactDamage(proj) { return false; }
    // Mude hitbox (Rectangle) para o dano usar outra area.
    ModifyDamageHitbox(proj, hitbox) {}
    // true/false: corta grama, teia...; undefined = o do jogo.
    CanCutTiles(proj) { return undefined; }
    CutTiles(proj) {}
    // A cor final do projetil (uma Color), ou undefined para a do jogo.
    GetAlpha(proj, lightColor) { return undefined; }
    // Desenho: false no PreDraw nao desenha o do jogo (desenhe o seu aqui,
    // com Main.EntitySpriteDraw). PostDraw roda depois do desenho do jogo.
    PreDraw(proj, lightColor) { return true; }
    PostDraw(proj, lightColor) {}
    // Gancho de escalar. No MOLDE, antes de o projetil existir: false impede
    // o lancamento; UseGrapple devolve o tipo a lancar (ou o mesmo).
    CanUseGrapple(player, type) { return true; }
    UseGrapple(player, type) { return type; }
    // true/false: agarra neste bloco; undefined = o do jogo (bloco solido).
    GrappleCanLatchOnTo(proj, player, tile) { return undefined; }

    // Copia os valores de um projetil do jogo para este.
    CloneDefaults(type) {
        const source = Terraria.Projectile.new();
        source['void .ctor()']();
        source['void SetDefaults(int Type)'](type);
        for (const key of CLONED_PROJECTILE_FIELDS) {
            try { this.Projectile[key] = source[key]; } catch (e) { /* campo que esta versao nao tem */ }
        }
    }

    // Os padroes do jogo para cada familia de projetil segurado.
    DefaultToSpear() { this.Projectile['void DefaultToSpear()'](); }
    DefaultToYoyo() { this.Projectile['void DefaultToYoyo()'](); }
    DefaultToFlail() { this.Projectile['void DefaultToFlail()'](); }
    DefaultToWhip() { this.Projectile['void DefaultToWhip()'](); }
    DefaultToDrillOrChainsaw() { this.Projectile['void DefaultToDrillOrChainsaw()'](); }
    DefaultToKite() { this.Projectile['void DefaultToKite()'](); }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModProjectile)) {
            throw new TypeError('ModProjectile.register(Classe): passe a classe, que estende ModProjectile');
        }
        autoloadGores();
        defineEntityField(Terraria.Projectile, 'ModProjectile');
        const inst = new cls();
        adoptTemplate(cls, inst);
        const name = cls.name;
        const type = bl.projectiles.register({
            name,
            texture: texturePath(inst.Texture || name),
            displayName: inst.DisplayName || localized('ProjectileName', name) || name,
            setDefaults(proj) {
                const m = bindInstance(inst.Clone(proj), proj, 'ModProjectile');
                m.SetDefaults(proj);
                m.PostSetDefaults(proj);
            },
            setStaticDefaults(t) {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                bl.projectiles.setFrames(t, Terraria.Main.projFrames[t]);
            },
        });
        inst.Type = type;
        projectilesByType.set(type, inst);
        whenReady(() => inst.PostSetupContent());
        hookProjectile(cls);
        return type;
    }

    static isModType(type) { return bl.projectiles.isModProjectile(type); }
    static isModProjectile(proj) { return !!proj && bl.projectiles.isModProjectile(proj.type); }
    static getTypeByName(name) { return bl.projectiles.typeOf(name); }
    static getModProjectile(type) { return projectilesByType.get(type); }
    static getByName(name) { return projectilesByType.get(bl.projectiles.typeOf(name)); }
}

function hookProjectile(cls) {
    const Pr = Terraria.Projectile;
    const has = (name) => overrides(cls, ModProjectile, name);
    const self = { minType: FIRST_PROJECTILE };

    // Sempre: o AIType e o OnSpawn podem vir de qualquer projetil, e o AIType
    // so se sabe depois do SetDefaults.
    once('proj.AI', () => {
        Pr['void AI()'].hook((original, p) => {
            const m = projectileOf(p);
            if (!m) return original(p);
            const n = m.constructor.name;
            if (!m.__spawned) {
                m.__spawned = true;
                guard(n + '.OnSpawn', () => m.OnSpawn(p));
            }
            if (guard(n + '.PreAI', () => m.PreAI(p)) !== false) {
                const aiType = m.AIType | 0;
                if (aiType > 0) {
                    const type = p.type;
                    p.type = aiType;
                    try { original(p); } finally { p.type = type; }
                } else {
                    original(p);
                }
                guard(n + '.AI', () => m.AI(p));
            }
            guard(n + '.PostAI', () => m.PostAI(p));
        }, self);
    });

    // O dano base do lacaio e da sentinela: o jogo recalcula o dano deles a
    // cada quadro a partir do originalDamage, e so o preenche nos tipos dele.
    // Como o ApplyStatsFromSource do tModLoader: o dano do item que criou o
    // projetil, ou o dano com que ele nasceu.
    once('proj.OriginalDamage', () => {
        Pr['void ApplyStatsFromSource(IEntitySource spawnSource)'].hook((original, p, source) => {
            original(p, source);
            if (!projectileOf(p) || p.originalDamage !== 0) return;
            const item = source ? source.Item : undefined;
            p.originalDamage = item && item.damage >= 0 ? item.damage : p.damage;
        }, self);
    });

    // Um Kill dentro do HandleMovement e o choque com bloco, e o
    // OnTileCollide recebe a velocidade de antes dele. O jogo, antes desse
    // Kill, empurra o projetil mais uma velocidade (para morrer na parede); o
    // tModLoader pula essa resposta toda quando o OnTileCollide devolve false,
    // e aqui ela ja rodou: a posicao volta a ser a do comeco mais a velocidade
    // de agora (a que o OnTileCollide deixou, para quicar). Sem isto, a
    // sentinela afundava no chao 2 px a cada choque.
    if (has('OnTileCollide')) once('proj.Movement', () => {
        Pr['void HandleMovement(Vector2 wetVelocity)'].hook((original, p, wet) => {
            const m = projectileOf(p);
            if (!m) return original(p, wet);
            const outer = m.__moving;
            const start = Vector2.Clone(p.position);
            const moving = { velocity: Vector2.Clone(p.velocity), kept: false };
            m.__moving = moving;
            try {
                original(p, wet);
                if (moving.kept && p.active) p.position = Vector2.Add(start, p.velocity);
            } finally {
                m.__moving = outer;
            }
        }, self);
    });

    if (has('PreKill') || has('OnKill') || has('OnTileCollide')) once('proj.Kill', () => {
        const solid = Terraria.Collision['bool SolidCollision(Vector2 Position, int Width, int Height)'];
        Pr['void Kill()'].hook((original, p) => {
            const m = projectileOf(p);
            if (!m || !p.active) return original(p);
            const n = m.constructor.name;
            // Morte por bloco: o jogo mata o projetil que bate com tileCollide.
            // No movimento, e o choque (a velocidade ja foi zerada nele: vale
            // a de antes). Fora dele (a IA de alguns aiStyle mata ao bater),
            // olha se ha bloco logo a frente.
            if (p.tileCollide && overrides(m.constructor, ModProjectile, 'OnTileCollide')) {
                let hit = m.__moving ? m.__moving.velocity : undefined;
                if (!hit) {
                    const v = p.velocity;
                    const len = Math.hypot(v.X, v.Y) || 1;
                    const ahead = Vector2.new(p.position.X + v.X / len, p.position.Y + v.Y / len);
                    if (solid(ahead, p.width, p.height)) hit = Vector2.Clone(v);
                }
                if (hit && guard(n + '.OnTileCollide', () => m.OnTileCollide(p, hit)) === false) {
                    if (m.__moving) m.__moving.kept = true;
                    return undefined;
                }
            }
            const timeLeft = p.timeLeft;
            if (guard(n + '.PreKill', () => m.PreKill(p, timeLeft)) === false) {
                p.active = false;
                return undefined;
            }
            guard(n + '.OnKill', () => m.OnKill(p, timeLeft));
            return original(p);
        }, self);
    });

    if (has('OnHitNPC')) once('proj.OnHitNPC', () => {
        Pr['void StatusNPC(int i)'].hook((original, p, i) => {
            original(p, i);
            const m = projectileOf(p);
            if (m) guard(m.constructor.name + '.OnHitNPC', () => m.OnHitNPC(p, Terraria.Main.npc[i]));
        }, self);
    });

    if (has('OnHitPlayer')) once('proj.OnHitPlayer', () => {
        Pr['void StatusPlayer(Player player)'].hook((original, p, player) => {
            original(p, player);
            const m = projectileOf(p);
            if (m) guard(m.constructor.name + '.OnHitPlayer', () => m.OnHitPlayer(p, player));
        }, self);
    });

    if (has('Colliding')) once('proj.Colliding', () => {
        Pr['bool Colliding(Rectangle myRect, Rectangle targetRect)'].hook((original, p, mine, target) => {
            const m = projectileOf(p);
            const r = m ? guard(m.constructor.name + '.Colliding', () => m.Colliding(p, mine, target)) : undefined;
            return typeof r === 'boolean' ? r : original(p, mine, target);
        }, self);
    });

    if (has('CanDamage')) once('proj.CanDamage', () => {
        Pr['void Damage()'].hook((original, p) => {
            const m = projectileOf(p);
            if (m && guard(m.constructor.name + '.CanDamage', () => m.CanDamage(p)) === false) return undefined;
            return original(p);
        }, self);
    });

    // O Damage do jogo sai no comeco para todo Main.projPet (le a tabela uma
    // vez so, ali): desligado so durante a chamada, o lacaio fere ao encostar.
    if (has('MinionContactDamage')) once('proj.MinionContactDamage', () => {
        Pr['void Damage()'].hook((original, p) => {
            const m = projectileOf(p);
            const pet = Terraria.Main.projPet;
            const type = p.type;
            if (!m || !pet[type] ||
                guard(m.constructor.name + '.MinionContactDamage', () => m.MinionContactDamage(p)) !== true) {
                return original(p);
            }
            pet[type] = false;
            try { return original(p); } finally { pet[type] = true; }
        }, self);
    });

    if (has('ModifyDamageHitbox')) once('proj.Hitbox', () => {
        Pr['Rectangle Damage_GetHitbox()'].hook((original, p) => {
            const box = original(p);
            const m = projectileOf(p);
            if (m) {
                const r = Rectangle.new(box.X, box.Y, box.Width, box.Height);
                guard(m.constructor.name + '.ModifyDamageHitbox', () => m.ModifyDamageHitbox(p, r));
                return r;
            }
            return box;
        }, self);
    });

    if (has('CanCutTiles')) once('proj.CanCutTiles', () => {
        Pr['bool CanCutTiles()'].hook((original, p) => {
            const m = projectileOf(p);
            const r = m ? guard(m.constructor.name + '.CanCutTiles', () => m.CanCutTiles(p)) : undefined;
            return typeof r === 'boolean' ? r : original(p);
        }, self);
    });

    if (has('CutTiles')) once('proj.CutTiles', () => {
        Pr['void CutTiles()'].hook((original, p) => {
            original(p);
            const m = projectileOf(p);
            if (m) guard(m.constructor.name + '.CutTiles', () => m.CutTiles(p));
        }, self);
    });

    if (has('GetAlpha')) once('proj.GetAlpha', () => {
        Pr['Color GetAlpha(Color newColor)'].hook((original, p, color) => {
            const m = projectileOf(p);
            const c = m ? guard(m.constructor.name + '.GetAlpha', () => m.GetAlpha(p, color)) : undefined;
            return c || original(p, color);
        }, self);
    });

    if (has('GrappleCanLatchOnTo')) once('proj.Latch', () => {
        Pr['bool AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile theTile)'].hook((original, p, tile) => {
            const vanilla = original(p, tile);
            const m = projectileOf(p);
            if (!m) return vanilla;
            const r = guard(m.constructor.name + '.GrappleCanLatchOnTo',
                            () => m.GrappleCanLatchOnTo(p, Terraria.Main.player[p.owner], tile));
            return typeof r === 'boolean' ? r : vanilla;
        }, self);
    });

    // O item de gancho lanca o item.shoot: filtro nativo pelo shoot do Item.
    if (has('CanUseGrapple') || has('UseGrapple')) once('proj.Grapple', () => {
        Terraria.Player['void FireGrapple(Item grappleItem)'].hook((original, player, item) => {
            const template = projectilesByType.get(item.shoot);
            if (!template) return original(player, item);
            const n = template.constructor.name;
            const shoot = item.shoot;
            if (guard(n + '.CanUseGrapple', () => template.CanUseGrapple(player, shoot)) === false) return undefined;
            const t = guard(n + '.UseGrapple', () => template.UseGrapple(player, shoot));
            const type = typeof t === 'number' ? t : shoot;
            if (type === shoot) return original(player, item);
            item.shoot = type;
            try { original(player, item); } finally { item.shoot = shoot; }
            return undefined;
        }, { minType: FIRST_PROJECTILE, on: 0, field: 'shoot' });
    });

    if (has('PreDraw') || has('PostDraw')) once('proj.Draw', () => {
        const lightAt = Terraria.Lighting['Color GetColor(int x, int y)'];
        Terraria.Main['void DrawProjDirect(Projectile proj, Player overridePlayer)'].hook((original, main, p, player) => {
            const m = projectileOf(p);
            if (!m) return original(main, p, player);
            const n = m.constructor.name;
            const c = p.Center;
            // Um projetil pode passar um quadro com a posicao NaN (a lanca com
            // itemAnimationMax 0 ao trocar de item): luz cheia, sem ler o tile.
            const light = Number.isFinite(c.X) && Number.isFinite(c.Y)
                ? lightAt(Math.floor(c.X / 16), Math.floor(c.Y / 16)) : Color.White;
            if (guard(n + '.PreDraw', () => m.PreDraw(p, light)) === false) return undefined;
            original(main, p, player);
            guard(n + '.PostDraw', () => m.PostDraw(p, light));
            return undefined;
        }, { minType: FIRST_PROJECTILE, on: 0 });
    });
}

// ================================ ModNPC ================================

// A tabela de drop de um NPC, como o NPCLoot do ExMod: Add(regra).
class NPCLoot {
    constructor(type) { this.type = type; }
    Add(rule) {
        Terraria.Main.ItemDropsDB['IItemDropRule RegisterToNPC(int type, IItemDropRule entry)'](this.type, rule);
        return rule;
    }
}

// Onde e quando um NPC vai nascer, para o SpawnChance: o NPCSpawnInfo do
// ExMod (que vem do tModLoader), sem os campos que precisam ler o tile.
class NPCSpawnInfo {
    constructor(x, y, player) {
        this.SpawnTileX = Math.floor(x / 16);
        this.SpawnTileY = Math.floor(y / 16);
        this.Player = player;
    }

    get Sky() { return this.Player.ZoneSkyHeight; }
    get Surface() { return this.Player.ZoneOverworldHeight; }
    get Underground() { return this.Player.ZoneDirtLayerHeight; }
    get Cavern() { return this.Player.ZoneRockLayerHeight; }
    get Underworld() { return this.Player.ZoneUnderworldHeight; }
    get AboveSurface() { return this.Surface || this.Sky; }
    get BelowSurface() { return !this.AboveSurface && !this.Underworld; }

    get Day() { return Terraria.Main.dayTime; }
    get Night() { return !Terraria.Main.dayTime; }
    get Rain() { return Terraria.Main.raining; }
    get SlimeRain() { return Terraria.Main.slimeRain; }
    get BloodMoon() { return Terraria.Main.bloodMoon; }
    get SolarEclipse() { return Terraria.Main.eclipse; }
    get PumpkinMoon() { return Terraria.Main.pumpkinMoon; }
    get FrostMoon() { return Terraria.Main.snowMoon; }
    get AnyEvent() { return this.SlimeRain || this.SolarEclipse || this.PumpkinMoon || this.FrostMoon; }

    get HardMode() { return Terraria.Main.hardMode; }
    get Expert() { return Terraria.Main.expertMode; }
    get Master() { return Terraria.Main.masterMode; }

    get Corruption() { return this.Player.ZoneCorrupt && this.AboveSurface; }
    get UndergroundCorruption() { return this.Player.ZoneCorrupt && (this.Underground || this.Cavern); }
    get Crimson() { return this.Player.ZoneCrimson && this.AboveSurface; }
    get UndergroundCrimson() { return this.Player.ZoneCrimson && (this.Underground || this.Cavern); }
    get Hallow() { return this.Player.ZoneHallow && this.AboveSurface; }
    get UndergroundHallow() { return this.Player.ZoneHallow && (this.Underground || this.Cavern); }
    get Snow() { return this.Player.ZoneSnow && this.AboveSurface; }
    get Ice() { return this.Player.ZoneSnow && (this.Underground || this.Cavern); }
    get Jungle() { return this.Player.ZoneJungle && this.AboveSurface; }
    get UndergroundJungle() { return this.Player.ZoneJungle && (this.Underground || this.Cavern); }
    get SurfaceMushroom() { return this.Player.ZoneGlowshroom && this.Surface; }
    get Mushroom() { return this.Player.ZoneGlowshroom && this.Cavern; }
    get Ocean() { return this.Player.ZoneBeach; }
    get Meteor() { return this.Player.ZoneMeteor; }
    get Desert() { return this.Player.ZoneDesert; }
    get DesertCave() { return this.Player.ZoneUndergroundDesert; }
    get Marble() { return this.Player.ZoneMarble; }
    get Granite() { return this.Player.ZoneGranite; }
    get Graveyard() { return this.Player.ZoneGraveyard; }
    get Dungeon() { return this.Player.ZoneDungeon; }
    get Lihzahrd() { return this.Player.ZoneLihzhardTemple; }

    get Invasion() { return Terraria.Main.invasionType > 0; }
    get AnyTower() {
        const p = this.Player;
        return p.ZoneTowerSolar || p.ZoneTowerVortex || p.ZoneTowerNebula || p.ZoneTowerStardust;
    }
    get CommonEnemy() { return !this.Invasion && !this.AnyEvent && !this.AnyTower; }
}

const npcsByType = new Map();
const npcOf = (npc) => instanceOf(npc, 'ModNPC', npcsByType);
// Os que tem SpawnChance: o sorteio do spawn natural olha so estes.
const spawnable = [];

class ModNPC {
    // O NPC do jogo desta instancia (no molde, undefined).
    get NPC() { return entityOf(this); }
    // A instancia de uma entidade nova, a partir desta (o molde, ou a de um
    // item que o jogo copiou). Sobrescreva para copiar fundo o que for seu.
    Clone(newNPC) { return cloneInstance(this); }
    Type = undefined;
    // true: fora do Mod Menu (o jeito do tModLoader tambem vale: um
    // NPCBestiaryDrawModifiers com Hide = true no NPCID.Sets.NPCBestiaryDrawOffset).
    HideFromModMenu = false;
    // Anima como este NPC do jogo (0 = nao anima). Pode vir do SetDefaults.
    AnimationType = 0;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: NPCName.<Classe> em
    // Localization/*.json, e sem isso o nome da classe.
    DisplayName = '';
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;
    // true: sem entrada no Bestiario.
    HideFromBestiary = false;
    // A cabeca (morador: mapa e menu de casas), relativa a Textures/, sem
    // .png. Padrao: a Texture + '_Head'; sem o arquivo, sem cabeca. Morador
    // sem cabeca nunca se muda (como no tModLoader).
    HeadTexture = '';
    // A cabeca depois do shimmer. Padrao: a Texture + '_Shimmer_Head'.
    ShimmerHeadTexture = '';
    // A musica enquanto ele esta perto da tela: MusicLoader.GetMusicSlot(...)
    // ou um MusicID do jogo. -1 = a do jogo. Pode mudar na IA (fase 2).
    Music = -1;
    // Entre dois NPCs com musica, ganha o de prioridade maior.
    SceneEffectPriority = SceneEffectPriority.BossLow;

    // Uma vez, com o tipo e a tabela de drop ja no jogo.
    // Main.npcFrameCount[this.Type] escrito aqui vale como a quantidade de
    // quadros da textura.
    SetStaticDefaults() {}
    // Vida, dano e defesa de base, e a escala de dificuldade, saem DEPOIS dele.
    SetDefaults(npc) {}
    PostStaticDefaults() {}
    PostSetDefaults(npc) {}
    PostSetupContent() {}
    // Depois do SetDefaults, para cada NPC: npc.buffImmune[...] = true.
    ApplyBuffImmunity(npc) {}
    // Uma vez: npcLoot.Add(ItemDropRule...).
    ModifyNPCLoot(npcLoot) {}
    // A entrada do Bestiario: bestiaryEntry.Info.Add(...). Uma vez.
    SetBestiary(database, bestiaryEntry) {}
    // A cada acerto, depois do efeito do jogo.
    HitEffect(npc, hitDirection, damage) {}

    // IA: false no PreAI pula a IA do jogo (a do aiStyle) e o AI.
    PreAI(npc) { return true; }
    AI(npc) {}
    PostAI(npc) {}
    // Animacao propria (no lugar do AnimationType): mexa em npc.frame.
    FindFrame(npc, frameHeight) {}
    // false: nao some por estar longe do jogador.
    CheckActive(npc) { return true; }
    // Morte: false no PreKill tira o drop; OnKill depois do drop.
    PreKill(npc) { return true; }
    OnKill(npc) {}

    // Spawn natural: o peso deste NPC no sorteio (o do jogo pesa 1). 0 = nao nasce.
    SpawnChance(spawnInfo) { return 0; }
    // Como nasce quando sorteado. Devolve o indice em Main.npc.
    SpawnNPC(spawnX, spawnY) {
        return Terraria.NPC.NewNPC(Terraria.NPC.GetSpawnSourceForNaturalSpawn(),
                                   Math.floor(spawnX), Math.floor(spawnY), this.Type, 0, 0, 0, 0, 0, 255);
    }

    // ---- morador (npc.townNPC = true no SetDefaults, aiStyle 7) ----
    // Pode se mudar agora? Checado de tempos em tempos pelo jogo, so enquanto
    // nao ha um deste tipo no mundo. numTownNPCs: moradores vivos.
    CanTownNPCSpawn(numTownNPCs) { return false; }
    // A sala serve? (left, right, top, bottom) em tiles. true = qualquer sala valida.
    CheckConditions(left, right, top, bottom) { return true; }
    // Nomes proprios: um e sorteado quando ele chega. Vazio: o nome do tipo.
    SetNPCNameList() { return []; }
    // A fala ao conversar (texto), ou undefined para a do jogo.
    GetChat(npc) { return undefined; }
    // O indice da cabeca em TextureAssets.NpcHead (-1 sem cabeca).
    NPCHeadSlot() { return bl.npcs.headSlot(this.Type); }
    // Os botoes da conversa (o celular mostra ate dois): buttons.button e
    // buttons.button2 recebem o texto; vazio = sem botao.
    SetChatButtons(npc, buttons) {}
    // Tocou num botao (firstButton: o primeiro). Devolva o nome de uma loja
    // registrada deste NPC (NPCShop) para abri-la.
    OnChatButtonClicked(npc, firstButton) { return undefined; }
    // As lojas, uma vez: new NPCShop(this.Type, 'Shop').Add(tipo).Register().
    AddShops() {}
    // Ataque do morador (NPCID.Sets.AttackType 0 arremesso, 1 tiro, 2 magia,
    // com AttackTime, AttackAverageChance e DangerDetectRange): o jogo escolhe
    // o alvo, a hora e a animacao; o projetil que ele dispara passa por aqui.
    // attack.damage e attack.knockback.
    TownNPCAttackStrength(npc, attack) {}
    // attack.projType: o projetil (o jogo nao tem um para morador de mod);
    // attack.attackDelay: em que quadro do ataque ele sai (padrao 1).
    TownNPCAttackProj(npc, attack) {}
    // attack.speed (a velocidade do tiro), attack.gravityCorrection (quanto
    // mirar acima do alvo) e attack.randomOffset (espalhamento).
    TownNPCAttackProjSpeed(npc, attack) {}
    // Gostos e desgostos do morador (felicidade e preco da loja), no
    // SetStaticDefaults: this.Happiness.SetNPCAffection(NPCID.Nurse, AffectionLevel.Love).
    get Happiness() { return new NPCHappiness(this.Type); }

    static NPCValue(p = 0, g = 0, s = 0, c = 0) {
        return p * 1000000 + g * 10000 + s * 100 + c;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModNPC)) {
            throw new TypeError('ModNPC.register(Classe): passe a classe, que estende ModNPC');
        }
        autoloadGores();
        defineEntityField(Terraria.NPC, 'ModNPC');
        const inst = new cls();
        adoptTemplate(cls, inst);
        const name = cls.name;
        let animation = inst.AnimationType | 0;
        let type = -1;
        let townNpc = false;
        const def = {
            name,
            texture: texturePath(inst.Texture || name),
            head: texturePath(inst.HeadTexture || (inst.Texture || name) + '_Head'),
            shimmerHead: texturePath(inst.ShimmerHeadTexture || (inst.Texture || name) + '_Shimmer_Head'),
            animationType: animation,
            displayName: inst.DisplayName || localized('NPCName', name) || name,
            setDefaults(npc) {
                const m = bindInstance(inst.Clone(npc), npc, 'ModNPC');
                m.SetDefaults(npc);
                m.ApplyBuffImmunity(npc);
                m.PostSetDefaults(npc);
                trackMusicNpc(npc, m);
                townNpc = !!npc.townNPC;
                // O AnimationType costuma vir de dentro do SetDefaults.
                const now = m.AnimationType | 0;
                if (now !== animation && type >= 0) {
                    animation = now;
                    inst.AnimationType = now;
                    bl.npcs.setAnimationType(type, now);
                }
            },
            setStaticDefaults(t) {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                bl.npcs.setFrames(t, Terraria.Main.npcFrameCount[t]);
                applyMenuVisibility('npc', inst, t, () => {
                    const drawn = Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset;
                    return drawn.ContainsKey(t) && drawn.get_Item(t).Hide;
                });
                inst.ModifyNPCLoot(new NPCLoot(t));
            },
        };
        // So quem escreveu HitEffect paga o hook dele.
        if (overrides(cls, ModNPC, 'HitEffect')) {
            def.hitEffect = (npc, hitDirection, damage) => {
                const m = npcOf(npc);
                if (m) m.HitEffect(npc, hitDirection, damage);
            };
        }
        type = bl.npcs.register(def);
        inst.Type = type;
        npcsByType.set(type, inst);
        // O nome de busca do jogo (NPCID.Search), como o do tModLoader:
        // 'ExampleMod/ExamplePerson'. A felicidade monta com ele a chave dos
        // textos (TownNPCMood_<nome>.<texto>), e sem ele o jogo lanca excecao.
        const searchName = String(bl.mod.name).replace(/\s+/g, '') + '/' + name;
        const moods = townMoodTexts(name);
        const looks = townLookFiles(inst.Texture || name);
        if (overrides(cls, ModNPC, 'SpawnChance')) spawnable.push(inst);
        whenReady(() => {
            // A amostra do jogo ja passou pelo SetDefaults do mod: e dela que
            // sai se e morador (o setDefaults acima pode ainda nao ter rodado).
            townNpc = townNpc || guard(name + ' amostra', () =>
                !!Terraria.ID.ContentSamples.NpcsByNetId.get_Item(type).townNPC) === true;
            guard(name + ' NPCID.Search', () => {
                const search = Terraria.ID.NPCID.Search;
                if (!search.ContainsName(searchName)) search['void Add(string name, int id)'](searchName, type);
            });
            for (const [key, texts] of moods) {
                ModLocalization.Register('TownNPCMood_' + searchName + '.' + key, texts);
                ModLocalization.Register('TownNPCMood_' + searchName + 'Transformed.' + key, texts);
            }
            if (!inst.HideFromBestiary) guard(name + '.SetBestiary', () => registerBestiary(inst, townNpc));
            if (overrides(cls, ModNPC, 'AddShops')) guard(name + '.AddShops', () => inst.AddShops());
            if (townNpc) guard(name + ' (perfil de morador)', () => setupTownLooks(inst, looks));
            inst.PostSetupContent();
        });
        hookNpc(cls);
        return type;
    }

    static isModType(type) { return bl.npcs.isModNpc(type); }
    static isModNPC(npc) { return !!npc && bl.npcs.isModNpc(npc.type); }
    static getTypeByName(name) { return bl.npcs.typeOf(name); }
    static getModNPC(type) { return npcsByType.get(type); }
    static getByName(name) { return npcsByType.get(bl.npcs.typeOf(name)); }
}

// A entrada do Bestiario, como o NPCLoader do ExMod: Enemy/Critter/TownNPC,
// o SetBestiary do mod, e os drops da tabela.
let bestiaryAdded = 0;
function registerBestiary(inst, townNpc) {
    const { BestiaryEntry } = Terraria.GameContent.Bestiary;
    const type = inst.Type;
    let entry;
    if (townNpc) entry = BestiaryEntry.TownNPC(type);
    else if (Terraria.ID.NPCID.Sets.CountsAsCritter[type]) entry = BestiaryEntry.Critter(type);
    else entry = BestiaryEntry.Enemy(type);
    const db = Terraria.Main.BestiaryDB;
    // O jogo ja cria sozinho uma entrada generica para cada NPC das amostras
    // (o populador do Bestiario roda depois de os NPCs de mod existirem): sem
    // tirar, ficavam duas — a dele sem fundo, texto nem quadro certo.
    const auto = db['BestiaryEntry FindEntryByNPCID(int npcNetId)'](type);
    if (auto && db.Entries.Contains(auto)) db.Entries.Remove(auto);
    inst.SetBestiary(db, entry);
    db['BestiaryEntry Register(BestiaryEntry entry)'](entry);
    db['void ExtractDropsForNPC(ItemDropDatabase dropsDatabase, int npcId)'](Terraria.Main.ItemDropsDB, type);
    bestiaryAdded++;
}

let bestiaryFinished = false;
function finishBestiary() {
    if (bestiaryFinished || !bestiaryAdded) return;
    bestiaryFinished = true;
    guard('Bestiario', () => Terraria.ID.ContentSamples['void CreateBestiarySortingIds(BestiaryDatabase database)'](Terraria.Main.BestiaryDB));
    bl.log('Bestiario: ' + bestiaryAdded + ' NPC(s) de mod');
}

// ============================== Felicidade ==============================

// Os niveis do jogo (Terraria.GameContent.Personalities.AffectionLevel).
const AffectionLevel = Object.freeze({ Hate: -100, Dislike: -50, Like: 50, Love: 100 });

// Gostos do morador, no banco de personalidades do proprio jogo
// (Main.ShopHelper._database): o jogo calcula felicidade e preco com eles,
// como faz com os moradores dele. Como o NPCHappiness do tModLoader.
class NPCHappiness {
    constructor(npcType) { this.NpcType = npcType; }

    static database() { return Terraria.Main.ShopHelper._database; }

    SetNPCAffection(npcType, level) {
        const P = Terraria.GameContent.Personalities;
        const trait = P.NPCPreferenceTrait.new();
        trait['void .ctor()']();
        trait.Level = level;
        trait.NpcId = npcType;
        NPCHappiness.database()['void Register(int npcId, IShopPersonalityTrait trait)'](this.NpcType, trait);
        return this;
    }

    // biome: 'Forest', 'Desert', 'Snow', 'Jungle', 'Ocean', 'Underground',
    // 'Hallow', 'Mushroom', 'Dungeon', 'Corruption', 'Crimson'.
    SetBiomeAffection(biome, level) {
        const P = Terraria.GameContent.Personalities;
        const name = String(biome).endsWith('Biome') ? String(biome) : biome + 'Biome';
        const shopping = P[name].new();
        shopping['void .ctor()']();
        const list = P.BiomePreferenceListTrait.new();
        list['void .ctor()']();
        const preference = P.BiomePreferenceListTrait.BiomePreference.new();
        preference['void .ctor(AffectionLevel affection, AShoppingBiome biome)'](level, shopping);
        list['void Add(BiomePreferenceListTrait.BiomePreference preference)'](preference);
        NPCHappiness.database()['void Register(int npcId, IShopPersonalityTrait trait)'](this.NpcType, list);
        return this;
    }
}

// TownNPCMood.<Classe> de Localization/<cultura>.json: [[texto, {cultura:
// texto}], ...]. O {0} do ExMod vira o {BiomeName}/{NPCName} do jogo.
function townMoodTexts(className) {
    const byKey = new Map();
    for (const c of CULTURES) {
        const json = bl.readJson('Localization/' + c + '.json');
        const moods = json && json.TownNPCMood && json.TownNPCMood[className];
        if (!moods || typeof moods !== 'object') continue;
        for (const [key, text] of Object.entries(moods)) {
            if (typeof text !== 'string') continue;
            const slot = /Biome$/.test(key) ? '{BiomeName}' : /NPC$/.test(key) ? '{NPCName}' : '{0}';
            if (!byKey.has(key)) byKey.set(key, {});
            byKey.get(key)[c] = text.replace(/\{0\}/g, slot);
        }
    }
    return byKey;
}

// ========================== Aparencia do morador ==========================

// As texturas do morador ao lado da Texture: _Party (festa), _Shimmer,
// _Shimmer_Party e o retrato da conversa, _Portrait e _Shimmer_Portrait. Como
// o NPCLoader do TL 1.7.1: um perfil do jogo (TownNPCProfiles, que escolhe a
// textura por festa e shimmer e a cabeca por variante) e o retrato em
// NPCID.Sets.NPCPortraits (sem ele, a conversa mostra o quadro do sprite).
// No registro, com o mod na pilha: caminho relativo e do mod que chama, e no
// bl.onContentReady quem chama e este arquivo. Guarda os absolutos que existem.
const LOOK_SUFFIXES = ['_Party', '_Shimmer', '_Shimmer_Party', '_Portrait', '_Shimmer_Portrait'];
function townLookFiles(base) {
    const files = {};
    for (const suffix of LOOK_SUFFIXES) {
        const rel = texturePath(base + suffix);
        if (bl.file.exists(rel)) files[suffix] = bl.mod.path + '/' + rel;
    }
    return files;
}

function setupTownLooks(inst, files) {
    const type = inst.Type;
    const has = (suffix) => suffix in files;
    const asset = (suffix) => bl.loadTextureAsset(files[suffix]);
    const head = bl.npcs.headSlot(type);
    const shimmerHead = bl.npcs.headSlot(type, true);

    if (has('_Party') || has('_Shimmer')) {
        const Profiles = Terraria.GameContent.TownNPCProfiles;
        const profile = Profiles['ITownNPCProfile LegacyWithSimpleShimmer(string subPath, int headIdNormal, int headIdShimmered, bool uniquePartyTexture, bool uniquePartyTextureShimmered)'](
            inst.constructor.name, head, shimmerHead >= 0 ? shimmerHead : head, true, true);
        const normal = Terraria.GameContent.TextureAssets.Npc[type];
        const plain = profile._profiles[0], shimmer = profile._profiles[1];
        plain._defaultNoAlt = normal;
        plain._defaultParty = has('_Party') ? asset('_Party') : normal;
        shimmer._defaultNoAlt = has('_Shimmer') ? asset('_Shimmer') : normal;
        shimmer._defaultParty = has('_Shimmer_Party') ? asset('_Shimmer_Party') : shimmer._defaultNoAlt;
        Profiles.Instance._townNPCProfiles.Add(type, profile);
    }

    if (has('_Portrait')) {
        const Sets = Terraria.ID.NPCID.Sets;
        const portrait = (suffix) => {
            const p = Sets.BasicPortrait('Images/TownNPCs/Portraits/Portrait_Guide');
            p._image = asset(suffix);
            return p;
        };
        let provider = Sets.PrioritizedPortrait();
        if (has('_Shimmer_Portrait')) {
            // A condicao "depois do shimmer" do Guia vale para qualquer morador.
            const condition = Sets.NPCPortraits.get_Item(22)._entries.get_Item(0).Condition;
            provider = provider.With(condition, portrait('_Shimmer_Portrait'));
        }
        Sets.NPCPortraits.Add(type, provider.Default(portrait('_Portrait')));
    }
}

// ================================ ModGore ================================

// Gore de mod, como o GoreLoader do tModLoader: todo PNG em Textures/Gores/ do
// mod vira um tipo novo, depois dos do jogo, com o nome do arquivo. Achados no
// registro de conteudo (com o mod na pilha); instalados quando o jogo esta
// pronto: TextureAssets.Gore, GoreID.Sets e ChildSafety.SafeGore crescem. O
// GoreID.Count do jogo fica como esta (o Gore.NewGore so o usa para as gotas).
const goresByMod = new Map();     // uuid -> Map(nome -> tipo)
const pendingGores = [];          // { mod, name, file }
let goresInstalled = false;

function autoloadGores() {
    const mod = bl.mod && bl.mod.uuid;
    if (!mod || goresByMod.has(mod)) return;
    goresByMod.set(mod, new Map());
    let files = [];
    try { files = bl.directory.exists('Textures/Gores') ? bl.directory.listFiles('Textures/Gores') : []; } catch (e) { files = []; }
    for (const f of files) {
        if (!f.endsWith('.png')) continue;
        const name = f.slice(f.lastIndexOf('/') + 1, -4);
        pendingGores.push({ mod, name, file: bl.mod.path + '/' + f });
    }
    if (pendingGores.length) whenReady(installGores);
}

function installGores() {
    if (goresInstalled || pendingGores.length === 0) return;
    goresInstalled = true;
    const TA = Terraria.GameContent.TextureAssets;
    const Sets = Terraria.ID.GoreID.Sets;
    const Safety = Terraria.GameContent.ChildSafety;
    const first = TA.Gore.length;
    const total = first + pendingGores.length;
    TA.Gore = TA.Gore.cloneResized(total);
    Sets.SpecialAI = Sets.SpecialAI.cloneResized(total);
    Sets.DisappearSpeed = Sets.DisappearSpeed.cloneResized(total);
    Sets.DisappearSpeedAlpha = Sets.DisappearSpeedAlpha.cloneResized(total);
    Sets.IsDrip = Sets.IsDrip.cloneResized(total);
    Safety.SafeGore = Safety.SafeGore.cloneResized(total);
    pendingGores.forEach((g, i) => {
        const type = first + i;
        guard('gore ' + g.name, () => { TA.Gore[type] = bl.loadTextureAsset(g.file); });
        Sets.DisappearSpeed[type] = 1;
        Sets.DisappearSpeedAlpha[type] = 1;
        goresByMod.get(g.mod).set(g.name, type);
    });
    bl.log('gores de mod: ' + pendingGores.length + ' (tipos ' + first + '..' + (total - 1) + ')');
}

class ModGore {
    // O tipo do gore 'Textures/Gores/<nome>.png' deste mod, ou 0 (nenhum).
    static getTypeByName(name) {
        const map = goresByMod.get(bl.mod && bl.mod.uuid);
        return (map && map.get(name)) || 0;
    }
}

// ================================ NPCShop ================================

// Loja de morador de mod, como o NPCShop do tModLoader. Cada loja registrada
// ganha um indice livre de Main.shop (de 99 para baixo; as do jogo vao ate
// ~25) e o InventoryStorage.SetupShop desse indice e preenchido aqui.
const shopsByKey = new Map();     // "tipo/nome" -> NPCShop
const shopsByIndex = new Map();   // indice de Main.shop -> NPCShop
let nextShopIndex = 99;

class NPCShop {
    constructor(npcType, name = 'Shop') {
        this.NpcType = npcType;
        this.Name = name;
        this.Entries = [];
        this.Index = -1;
    }

    // Um item. options: { condition: () => bool, price: preco em cobre (ou na
    // moeda), currency: id de CustomCurrencyManager.RegisterCurrency }.
    Add(type, options = {}) {
        this.Entries.push({ type, ...options });
        return this;
    }

    Register() {
        const key = this.NpcType + '/' + this.Name;
        if (shopsByKey.has(key)) throw new Error('NPCShop: ' + key + ' ja registrada');
        this.Index = nextShopIndex--;
        shopsByKey.set(key, this);
        shopsByIndex.set(this.Index, this);
        hookShops();
        return this;
    }

    static get(npcType, name) { return shopsByKey.get(npcType + '/' + name); }

    // Abre esta loja para o jogador local, como o botao "Loja" do jogo.
    Open() {
        Terraria.Main.instance['void OpenShop(int shopIndex)'](this.Index);
        const pages = bl.classOf('', 'GUIInstance').Active.GUIPageIcons;
        pages['void OpenUI(GUIPageIcons.Category left, GUIPageIcons.Category right)'](2, 4);
    }
}

function hookShops() {
    once('npc.Shops', () => {
        Terraria.InventoryStorage['void SetupShop(int type)'].hook((original, self, type) => {
            const shop = shopsByIndex.get(type);
            original(self, type);
            if (!shop) return;
            const items = self.item;
            for (let i = 0; i < items.length; i++) items[i]['void SetDefaults(int Type, ItemVariant variant)'](0, null);
            let slot = 0;
            for (const e of shop.Entries) {
                if (slot >= items.length - 1) break;
                if (e.condition && guard('NPCShop ' + shop.Name, () => e.condition()) !== true) continue;
                const item = items[slot++];
                item['void SetDefaults(int Type, ItemVariant variant)'](e.type, null);
                item.isAShopItem = true;
                if (e.currency !== undefined) item.shopSpecialCurrency = e.currency;
                if (e.price !== undefined) item.shopCustomPrice = e.price;
            }
        });
    });
}

// O NPC com quem o jogador local conversa, e o ModNPC dele.
function talkingTo() {
    const player = Terraria.Main.player[Terraria.Main.myPlayer];
    const i = player ? player.talkNPC : -1;
    const npc = i >= 0 && i < Terraria.Main.npc.length - 1 ? Terraria.Main.npc[i] : null;
    const m = npc ? npcOf(npc) : undefined;
    return m ? { npc, m } : null;
}

// O Mercador: o morador de mod pega dele o icone de loja e o botao de felicidade.
const NPCID_MERCHANT = 17;

// A conversa do celular (GUINPCDialogue): o SetupButtonText e um switch pelo
// tipo do NPC que devolve, por ref, texto e icone de cada botao, custo e se
// mostra o botao de felicidade. Para o morador de mod ele roda como se fosse o
// Mercador, e o texto vem do SetChatButtons do mod.
function hookChatButtons() {
    once('npc.ChatButtons', () => {
        const Dialogue = bl.classOf('', 'GUINPCDialogue');
        Dialogue['void SetupButtonText(ref string focusText, ref Texture2D option1Tex, ref string focusText3, ref Texture2D option2Tex, ref int cost, ref bool showHappiness)'].hook(
            (original, self, text1, tex1, text2, tex2, cost, happy) => {
                const t = talkingTo();
                if (!t) return original(self, text1, tex1, text2, tex2, cost, happy);
                const type = t.npc.type;
                t.npc.type = NPCID_MERCHANT;
                try { original(self, text1, tex1, text2, tex2, cost, happy); } finally { t.npc.type = type; }
                const buttons = { button: '', button2: '' };
                guard(t.m.constructor.name + '.SetChatButtons', () => t.m.SetChatButtons(t.npc, buttons));
                const icon = tex1.value;
                text1.value = buttons.button || '';
                tex1.value = buttons.button ? icon : null;
                text2.value = buttons.button2 || '';
                tex2.value = buttons.button2 ? icon : null;
                cost.value = 0;
            });
        const clicked = (first) => (original, self, ...args) => {
            const t = talkingTo();
            if (!t) return original(self, ...args);
            const shopName = guard(t.m.constructor.name + '.OnChatButtonClicked', () => t.m.OnChatButtonClicked(t.npc, first));
            if (typeof shopName !== 'string') return undefined;
            const shop = NPCShop.get(t.npc.type, shopName);
            if (shop) shop.Open();
            else bl.log('NPCShop: ' + t.m.constructor.name + ' pediu a loja "' + shopName + '", nao registrada');
            return undefined;
        };
        Dialogue['void Option1Clicked(int healCost)'].hook(clicked(true));
        Dialogue['void Option2Clicked()'].hook(clicked(false));
    });
}

// Moradores vivos (o numTownNPCs do CanTownNPCSpawn). 368 = mercador viajante.
function countTownNPCs() {
    const npcs = Terraria.Main.npc;
    let n = 0;
    for (let i = 0; i < npcs.length - 1; i++) {
        const npc = npcs[i];
        if (npc.active && npc.townNPC && npc.type !== 368) n++;
    }
    return n;
}

function hookNpc(cls) {
    const N = Terraria.NPC;
    const has = (name) => overrides(cls, ModNPC, name);
    const self = { minType: FIRST_NPC };

    // ---- morador ----
    // Quem pode se mudar: o jogo zera Main.townNPCCanSpawn e marca os dele a
    // cada checagem (o contador checkForSpawns volta a 0 nela); os de mod sao
    // marcados logo depois, como no NPCLoader.CanTownNPCSpawn do tModLoader.
    if (has('CanTownNPCSpawn')) once('npc.TownSpawn', () => {
        const Main = Terraria.Main;
        const WorldGen = Terraria.WorldGen;
        const anyNPCs = N['bool AnyNPCs(int Type)'];
        Main['void UpdateTime_SpawnTownNPCs(bool forceUpdate)'].hook((original, force) => {
            original(force);
            if (Main.netMode === 1 || Main.checkForSpawns !== 0) return;
            let towns = -1;
            for (const [type, m] of npcsByType) {
                if (!overrides(m.constructor, ModNPC, 'CanTownNPCSpawn') || m.NPCHeadSlot() < 0 || anyNPCs(type)) continue;
                if (towns < 0) towns = countTownNPCs();
                if (guard(m.constructor.name + '.CanTownNPCSpawn', () => m.CanTownNPCSpawn(towns)) !== true) continue;
                Main.townNPCCanSpawn[type] = true;
                if (WorldGen.prioritizedTownNPCType === 0) WorldGen.prioritizedTownNPCType = type;
            }
        });
    });

    // A sala serve para ele (a mudanca e o menu de casas passam por aqui).
    if (has('CheckConditions')) once('npc.TownRoom', () => {
        const WorldGen = Terraria.WorldGen;
        WorldGen['bool CheckSpecialTownNPCSpawningConditions(int type)'].hook((original, type) => {
            const m = npcsByType.get(type);
            if (!m) return original(type);
            return guard(m.constructor.name + '.CheckConditions',
                () => m.CheckConditions(WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2)) !== false;
        });
    });

    if (has('SetNPCNameList')) once('npc.Names', () => {
        N['string getNewNPCName(int npcType)'].hook((original, type) => {
            const m = npcsByType.get(type);
            const names = m ? guard(m.constructor.name + '.SetNPCNameList', () => m.SetNPCNameList()) : undefined;
            if (!Array.isArray(names) || names.length === 0) return original(type);
            return String(names[Math.floor(Math.random() * names.length)]);
        });
    });

    if (has('SetChatButtons') || has('OnChatButtonClicked')) hookChatButtons();

    // O ataque: a IA 7 do jogo leva o morador ao estado de ataque (ai[0] 10
    // arremesso, 12 tiro, 14 magia) e conta localAI[3] ate a hora do tiro,
    // mas o projetil sai de um switch pelo tipo, e para tipo de mod nao sai
    // nada. Na hora em que o jogo atiraria (localAI[3] == attackDelay), o tiro
    // do mod sai daqui, no inimigo a vista mais perto, com o dano escalado
    // pelo jogo. Os TownNPCAttack* do tModLoader trocam os mesmos valores.
    if (has('TownNPCAttackProj')) once('npc.TownAttack', () => {
        const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
        const canHit = Terraria.Collision['bool CanHit(Vector2 Position1, int Width1, int Height1, Vector2 Position2, int Width2, int Height2)'];
        N['void AI()'].hook((original, npc) => {
            original(npc);
            const m = npcOf(npc);
            if (!m || !npc.townNPC || Terraria.Main.netMode === 1) return;
            const state = npc.ai[0];
            if (state !== 10 && state !== 12 && state !== 14) return;
            const n = m.constructor.name;
            const attack = { projType: 0, attackDelay: 1, damage: npc.damage, knockback: 3, speed: 10,
                             gravityCorrection: 0, randomOffset: 0 };
            guard(n + '.TownNPCAttackProj', () => m.TownNPCAttackProj(npc, attack));
            if (npc.localAI[3] !== attack.attackDelay || attack.projType <= 0) return;
            guard(n + '.TownNPCAttackStrength', () => m.TownNPCAttackStrength(npc, attack));
            guard(n + '.TownNPCAttackProjSpeed', () => m.TownNPCAttackProjSpeed(npc, attack));

            const range = Terraria.ID.NPCID.Sets.DangerDetectRange[npc.type] || 700;
            const here = npc.Center;
            let target = null, best = range;
            const npcs = Terraria.Main.npc;
            for (let i = 0; i < npcs.length - 1; i++) {
                const o = npcs[i];
                if (!o.active || o.friendly || o.damage <= 0 || o.townNPC) continue;
                const c = o.Center;
                const d = Math.hypot(c.X - here.X, c.Y - here.Y);
                if (d < best && canHit(npc.position, npc.width, npc.height, o.position, o.width, o.height)) {
                    best = d;
                    target = o;
                }
            }
            let dx = npc.spriteDirection, dy = 0;
            if (target) {
                const c = target.Center;
                dx = c.X - here.X;
                dy = c.Y - attack.gravityCorrection - here.Y;
                const len = Math.hypot(dx, dy) || 1;
                dx /= len;
                dy /= len;
            }
            let vx = dx * attack.speed, vy = dy * attack.speed;
            if (attack.randomOffset) {
                vx += (Math.random() * 2 - 1) * attack.randomOffset;
                vy += (Math.random() * 2 - 1) * attack.randomOffset;
            }
            const damage = npc['int GetAttackDamage_ForTownNPC(float normalDamage)'](attack.damage);
            const i = NewProjectile(npc.GetSpawnSource_ForProjectile(), here.X + npc.spriteDirection * 16, here.Y - 2,
                                    vx, vy, attack.projType, damage, attack.knockback, Terraria.Main.myPlayer, 0, 0, 0, null);
            const proj = Terraria.Main.projectile[i];
            if (proj) {
                proj.npcProj = true;
                proj.noDropItem = true;
            }
        }, self);
    });

    // A fala: a do jogo roda antes (o que ela registra continua), a do mod vale.
    if (has('GetChat')) once('npc.GetChat', () => {
        N['string GetChat()'].hook((original, npc) => {
            const chat = original(npc);
            const m = npcOf(npc);
            const text = m ? guard(m.constructor.name + '.GetChat', () => m.GetChat(npc)) : undefined;
            return typeof text === 'string' ? text : chat;
        }, self);
    });

    if (has('PreAI') || has('AI') || has('PostAI')) once('npc.AI', () => {
        N['void AI()'].hook((original, npc) => {
            const m = npcOf(npc);
            if (!m) return original(npc);
            const n = m.constructor.name;
            if (guard(n + '.PreAI', () => m.PreAI(npc)) !== false) {
                original(npc);
                guard(n + '.AI', () => m.AI(npc));
            }
            guard(n + '.PostAI', () => m.PostAI(npc));
        }, self);
    });

    if (has('FindFrame')) once('npc.FindFrame', () => {
        const heights = new Map();
        N['void FindFrame()'].hook((original, npc) => {
            const m = npcOf(npc);
            if (!m || !overrides(m.constructor, ModNPC, 'FindFrame')) return original(npc);
            let h = heights.get(npc.type);
            if (h === undefined) {
                const tex = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                h = tex ? Math.floor(tex.Height / Math.max(1, Terraria.Main.npcFrameCount[npc.type])) : 0;
                heights.set(npc.type, h);
            }
            guard(m.constructor.name + '.FindFrame', () => m.FindFrame(npc, h));
        }, self);
    });

    if (has('CheckActive')) once('npc.CheckActive', () => {
        N['void CheckActive()'].hook((original, npc) => {
            const m = npcOf(npc);
            if (m && guard(m.constructor.name + '.CheckActive', () => m.CheckActive(npc)) === false) return;
            original(npc);
        }, self);
    });

    if (has('PreKill') || has('OnKill')) once('npc.Kill', () => {
        N['void NPCLoot()'].hook((original, npc) => {
            const m = npcOf(npc);
            if (!m) return original(npc);
            const n = m.constructor.name;
            if (guard(n + '.PreKill', () => m.PreKill(npc)) === false) return;
            original(npc);
            guard(n + '.OnKill', () => m.OnKill(npc));
        }, self);
    });

    if (has('SpawnChance')) once('npc.Spawn', hookNaturalSpawn);
}

/**
 * Spawn natural, como o do ExMod: o SpawnNPC do jogo roda; se ele fez nascer
 * um NPC, sorteia entre esse (peso 1) e os de mod (peso = SpawnChance). Se sai
 * um de mod, ele toma o lugar, no mesmo ponto.
 */
function hookNaturalSpawn() {
    const Main = Terraria.Main;
    Terraria.NPC['void SpawnNPC()'].hook((original) => {
        const slot = bl.npcs.freeSlot();
        original();
        if (slot < 0) return;
        const npc = Main.npc[slot];
        if (!npc.active || npc.townNPC || npc.boss) return;
        const x = npc.Center.X, y = npc.Bottom.Y;
        const info = new NPCSpawnInfo(x, y, Main.player[Main.myPlayer]);
        const pool = [];
        let total = 1;
        for (const m of spawnable) {
            const w = Number(guard(m.constructor.name + '.SpawnChance', () => m.SpawnChance(info))) || 0;
            if (w > 0) { pool.push([m, w]); total += w; }
        }
        if (!pool.length) return;
        let r = Math.random() * total;
        if (r < 1) return;   // ficou o do jogo
        r -= 1;
        for (const [m, w] of pool) {
            if (r < w) {
                npc.active = false;
                guard(m.constructor.name + '.SpawnNPC', () => m.SpawnNPC(x, y));
                return;
            }
            r -= w;
        }
    });
}

// ============================ Sons e musicas ============================
//
// A Unity deste build nao cria AudioClip novo (o Strip Engine Code tirou o
// AudioClip.Create e as icalls por baixo dele), entao o audio de mod toca pelo
// Android: bl.sounds (SoundPool) e bl.music (MediaPlayer), ver ModAudio.kt. O
// jogo continua mandando em quando e quanto:
//
//  - SoundStyle, como o do tModLoader. O `new` devolve um LegacySoundStyle
//    MARCADOR do jogo (SoundId 1000, fora dos do jogo; Style = o indice do
//    som), porque e esse o tipo de Item.UseSound e de NPC.HitSound. Todo som
//    do jogo passa por LegacySoundPlayer.PlaySound(int ...): o hook ali toca o
//    nosso com o volume e o pan que o jogo daria (distancia ao centro da
//    tela, volume de efeitos).
//  - MusicLoader + ModNPC.Music, como o tModLoader: o NPC com musica mais
//    prioritario perto da tela ganha. A troca e a do jogo entre duas faixas:
//    a nova sobe 0,005 por quadro e a que tocava so comeca a descer (no
//    mesmo passo) quando a nova passa de 0,25 — ~4 s, sem corte. O fade
//    roda depois do Main.UpdateAudio, que roda sempre; a escolha, nos
//    UpdateAudio_DecideOn*Music, que ele pula com o volume de musica em 0.

const MOD_SOUND_ID = 1000;
const AUDIO_EXTENSIONS = ['.ogg', '.wav', '.mp3'];
const SoundLimitBehavior = Object.freeze({ IgnoreNew: 0, ReplaceOldest: 1 });
const SceneEffectPriority = Object.freeze({
    None: 0, BiomeLow: 1, BiomeMedium: 2, BiomeHigh: 3, Environment: 4, Event: 5,
    BossLow: 6, BossMedium: 7, BossHigh: 8,
});

// 'Sounds/Tiro' -> o arquivo, com ou sem extensao (.ogg, .wav, .mp3, como no
// tModLoader), na pasta do mod de quem chama (ou em `base`). null se nao ha.
function findAudioFile(base, path) {
    const rel = String(path);
    const root = base || (bl.mod && bl.mod.path);
    const names = /\.[a-z0-9]{2,4}$/i.test(rel) ? [rel] : AUDIO_EXTENSIONS.map((e) => rel + e);
    for (const name of names) {
        const file = name.startsWith('/') || !root ? name : bl.path.join(root, name);
        if (bl.file.exists(file)) return file;
    }
    return null;
}

function reportOnce(key, text) {
    if (reported.has(key)) return;
    reported.add(key);
    bl.log(text);
}

// ------------------------------- efeitos -------------------------------

const modSounds = [];              // indice (o Style do marcador) -> som
const soundStyles = new Map();     // arquivo + opcoes -> marcador
const soundIds = new Map();        // arquivo -> id do bl.sounds (um load por arquivo)
let attenuation = 0;

class SoundStyle {
    /**
     * new SoundStyle('Sounds/Tiro', { Volume, Pitch, PitchVariance,
     * MaxInstances, SoundLimitBehavior }), como o do tModLoader (as chaves
     * tambem valem em minusculas). Vai no Item.UseSound, no NPC.HitSound e no
     * SoundEngine.PlaySound. O mesmo arquivo com as mesmas opcoes devolve o
     * mesmo objeto: criar no SetDefaults nao custa nada.
     */
    constructor(path, options) {
        const o = options || {};
        const opt = (k, fallback) => {
            const v = o[k] !== undefined ? o[k] : o[k[0].toLowerCase() + k.slice(1)];
            return v === undefined ? fallback : v;
        };
        const s = {
            file: findAudioFile(null, path),
            volume: Number(opt('Volume', 1)),
            pitch: Number(opt('Pitch', 0)),
            pitchVariance: Number(opt('PitchVariance', 0)),
            maxInstances: opt('MaxInstances', 1) | 0,
            limit: opt('SoundLimitBehavior', SoundLimitBehavior.ReplaceOldest),
        };
        const key = [s.file || path, s.volume, s.pitch, s.pitchVariance, s.maxInstances, s.limit].join('|');
        const cached = soundStyles.get(key);
        if (cached) return cached;

        s.id = 0;
        if (!s.file) {
            // Sem lancar: um SoundStyle no SetDefaults que lancasse deixaria o
            // item pela metade. Fica mudo, e o log diz por que.
            reportOnce('som:' + path, "SoundStyle: nao achei '" + path + "' (" + AUDIO_EXTENSIONS.join(', ') + ') na pasta do mod');
        } else {
            s.id = soundIds.get(s.file) || 0;
            if (!s.id) {
                s.id = guard('SoundStyle ' + path, () => bl.sounds.load(s.file)) || 0;
                soundIds.set(s.file, s.id);
            }
        }
        s.duration = 0;
        s.playing = [];      // { stream, end } dos que ainda soam
        const index = modSounds.length;
        modSounds.push(s);
        const marker = Terraria.Audio.LegacySoundStyle.new();
        marker['void .ctor(int soundId, int style, SoundType type, int maxTrackedInstances)'](MOD_SOUND_ID, index, 0, 0);
        soundStyles.set(key, marker);
        hookModSounds();
        return marker;
    }
}

// O som de mod por tras de um marcador, ou undefined.
function modSoundOf(style) {
    if (!style || typeof style !== 'object' || style.SoundId !== MOD_SOUND_ID) return undefined;
    return modSounds[style.Style];
}

// Volume e pan como o LegacySoundPlayer: sem posicao (x = -1), cheio no meio;
// com posicao, cai com a distancia ao centro da tela ate o
// SoundAttenuationDistance; o pan e o lado da tela. O stream, ou 0.
function playModSound(s, x, y, volumeScale, pitchOffset) {
    if (!s || !s.id) return 0;
    const Main = Terraria.Main;
    let volume = 1, pan = 0;
    if (x !== -1 && y !== -1) {
        if (!attenuation) attenuation = Terraria.Audio.LegacySoundPlayer.SoundAttenuationDistance || 2500;
        const sp = Main.screenPosition;
        const halfW = Main.screenWidth / 2;
        const cx = sp.X + halfW, cy = sp.Y + Main.screenHeight / 2;
        const dist = Math.hypot(x - cx, y - cy);
        if (dist >= attenuation) return 0;
        pan = Math.max(-1, Math.min(1, (x - cx) / halfW));
        volume = 1 - dist / attenuation;
    }
    volume *= s.volume * volumeScale * Main.soundVolume;
    if (!(volume > 0)) return 0;

    // MaxInstances: quantos deste soam juntos (0 = sem limite).
    const now = Date.now();
    s.playing = s.playing.filter((p) => p.end > now);
    if (s.maxInstances > 0 && s.playing.length >= s.maxInstances) {
        if (s.limit === SoundLimitBehavior.IgnoreNew) return 0;
        bl.sounds.stop(s.playing.shift().stream);
    }
    // Tom em oitavas, como o SoundEffectInstance.Pitch: velocidade = 2^tom.
    const rate = Math.pow(2, s.pitch + (Math.random() - 0.5) * s.pitchVariance + pitchOffset);
    const stream = bl.sounds.play(s.id, volume * Math.min(1, 1 - pan), volume * Math.min(1, 1 + pan), rate);
    if (stream > 0) {
        if (!s.duration) s.duration = bl.sounds.duration(s.id) || 1000;
        s.playing.push({ stream, end: now + s.duration / Math.max(0.5, Math.min(2, rate)) });
    }
    return stream;
}

function hookModSounds() {
    once('sound.Play', () => {
        Terraria.Audio.LegacySoundPlayer['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'].hook(
            (original, self, type, x, y, style, volumeScale, pitchOffset) => {
                if (type !== MOD_SOUND_ID) return original(self, type, x, y, style, volumeScale, pitchOffset);
                playModSound(modSounds[style], x, y, volumeScale, pitchOffset);
                return null;
            });
    });
}

const SoundEngine = Object.freeze({
    /**
     * SoundEngine.PlaySound(estilo, posicao), como o do tModLoader: um
     * SoundStyle de mod ou um SoundID do jogo, na posicao (Vector2) ou, sem
     * ela, sem distancia. Som de mod devolve o stream (0 = nao tocou).
     */
    PlaySound(style, position) {
        const s = modSoundOf(style);
        if (s) return position ? playModSound(s, position.X, position.Y, 1, 0) : playModSound(s, -1, -1, 1, 0);
        const E = Terraria.Audio.SoundEngine;
        return position
            ? E['SoundEffectInstance PlaySound(LegacySoundStyle type, Vector2 position, float pitchOffset, float volumeScale)'](style, position, 0, 1)
            : E['SoundEffectInstance PlaySound(LegacySoundStyle type, int x, int y, float pitchOffset, float volumeScale)'](style, -1, -1, 0, 1);
    },
    // O stream mais novo deste SoundStyle que ainda soa, ou 0.
    FindActiveSound(style) {
        const s = modSoundOf(style);
        if (!s) return 0;
        const now = Date.now();
        s.playing = s.playing.filter((p) => p.end > now);
        return s.playing.length ? s.playing[s.playing.length - 1].stream : 0;
    },
    StopSound(stream) { if (stream > 0) bl.sounds.stop(stream); },
});

// ------------------------------- musica -------------------------------

// O primeiro slot de mod: MusicID.Count, como no tModLoader. Lido na
// primeira vez: o topo deste arquivo roda antes de o jogo estar pronto.
let musicBaseCache = 0;
const musicBase = () => musicBaseCache || (musicBaseCache = guard('MusicID.Count', () => Terraria.ID.MusicID.Count) || 105);
// O passo e o limiar do jogo (LegacyAudioSystem.UpdateCommonTrack e
// UpdateCommonTrackTowardStopping; isMainTrackAudible no Main.UpdateAudio):
// 0 a 1 em 200 quadros, e a faixa que sai espera a nova chegar a 0,25.
const MUSIC_FADE = 0.005;
const MUSIC_AUDIBLE = 0.25;
const musicTracks = [];              // slot - musicBase() -> { id, file, fade, sent }
const musicSlots = new Map();        // arquivo -> slot
const musicNpcs = new Set();         // NPCs de mod vivos (o ModNPC.Music pode mudar na IA)
let wantedMusic = -1;
let musicHooked = false;
let savedSilenceFade = null;         // o musicFade[0] do jogo, enquanto a nossa manda

// O NPC de mod entra na conta da musica no SetDefaults. Nasce com Music (um
// MusicID do jogo tambem, que nao passa pelo GetMusicSlot): liga a musica de
// mod. Sem Music, so entra se ela ja esta ligada (o Music pode mudar na IA).
// Sem musica de mod nenhuma, o conjunto nao guarda NPC nenhum: ele segura os
// objetos, e so a escolha da musica, a cada quadro, tira os mortos.
function trackMusicNpc(npc, m) {
    if ((m.Music | 0) >= 0) hookMusic();
    else if (!musicHooked) return;
    musicNpcs.add(npc);
}

const MusicLoader = Object.freeze({
    /**
     * O slot de uma musica do mod: GetMusicSlot('Music/Chefe') ou, como no
     * tModLoader, GetMusicSlot(mod, 'Music/Chefe'). Sem extensao ou com
     * (.ogg, .wav, .mp3). 0 se o arquivo nao existe. Vai no ModNPC.Music.
     */
    GetMusicSlot(modOrPath, path) {
        const base = path !== undefined && modOrPath && modOrPath.path ? modOrPath.path : null;
        const rel = path !== undefined ? path : modOrPath;
        const file = findAudioFile(base, rel);
        if (!file) {
            reportOnce('musica:' + rel, "MusicLoader: nao achei '" + rel + "' (" + AUDIO_EXTENSIONS.join(', ') + ') na pasta do mod');
            return 0;
        }
        let slot = musicSlots.get(file);
        if (slot) return slot;
        const id = guard('MusicLoader ' + rel, () => bl.music.register(file)) || 0;
        if (!id) return 0;
        slot = musicBase() + musicTracks.length;
        musicTracks.push({ id, file, fade: 0, sent: 0 });
        musicSlots.set(file, slot);
        hookMusic();
        return slot;
    },
    MusicExists(modOrPath, path) {
        const base = path !== undefined && modOrPath && modOrPath.path ? modOrPath.path : null;
        return !!findAudioFile(base, path !== undefined ? path : modOrPath);
    },
    // Esta musica de mod esta tocando agora?
    IsMusicPlaying(slot) {
        const t = musicTracks[slot - musicBase()];
        return !!t && bl.music.state(t.id) === 2;
    },
    get MusicCount() { return musicBase() + musicTracks.length; },
});

// Como o Main.UpdateAudio_DecideOnNewMusic do tModLoader: dos NPCs de mod com
// Music (>= 0) perto da tela (o retangulo dela com 5000 px de folga), o de
// maior SceneEffectPriority. -1 = nenhum.
function chooseModMusic() {
    const Main = Terraria.Main;
    if (Main.gameMenu || !musicNpcs.size) return -1;
    const sp = Main.screenPosition;
    const R = 5000;
    const x0 = sp.X - R, x1 = sp.X + Main.screenWidth + R;
    const y0 = sp.Y - R, y1 = sp.Y + Main.screenHeight + R;
    let best = -1, bestPriority = -1;
    for (const npc of musicNpcs) {
        // So o que esta no mundo: a amostra do ContentSamples tambem passa
        // pelo SetDefaults.
        const live = npc.active && Main.npc[npc.whoAmI] === npc;
        const m = live ? npcOf(npc) : undefined;
        if (!m) { musicNpcs.delete(npc); continue; }
        const music = m.Music | 0;
        if (music < 0) continue;
        const c = npc.Center;
        if (c.X < x0 || c.X > x1 || c.Y < y0 || c.Y > y1) continue;
        const priority = m.SceneEffectPriority | 0;
        if (priority > bestPriority) { best = music; bestPriority = priority; }
    }
    return best;
}

// O silencio do jogo (newMusic 0) enquanto a nossa toca. Com o musicFade[0]
// em 0, que e como o jogo o deixa, o curMusic 0 CORTA as faixas dele num
// quadro (UpdateCommonTrackTowardStopping); em 1 elas saem com o fade.
function silenceGame(on) {
    const fade = Terraria.Main.musicFade;
    if (on) {
        if (savedSilenceFade === null) savedSilenceFade = fade[0];
        fade[0] = 1;
        Terraria.Main.newMusic = 0;
    } else if (savedSilenceFade !== null) {
        fade[0] = savedSilenceFade;
        savedSilenceFade = null;
    }
}

// A escolha, depois da do jogo. A nossa so cala o jogo quando ja se ouve;
// ate la segura o que estava tocando, como o jogo faz ao trocar de faixa.
function decideMusic() {
    const Main = Terraria.Main;
    wantedMusic = chooseModMusic();
    const t = wantedMusic >= musicBase() ? musicTracks[wantedMusic - musicBase()] : null;
    if (t && t.fade > MUSIC_AUDIBLE) return silenceGame(true);
    silenceGame(false);
    if (t) Main.newMusic = Main.curMusic;
    else if (wantedMusic >= 0) Main.newMusic = wantedMusic;   // MusicID do jogo pedido por NPC de mod
}

// O fade das nossas, como o do jogo: a principal sobe; as outras so descem
// quando a principal ja se ouve. Sem faixa nenhuma do jogo (curMusic 0), a
// nossa desce na hora — o jogo cortaria.
function stepModMusic() {
    const Main = Terraria.Main;
    const target = Main.gameMenu ? -1 : wantedMusic;
    const main = target >= musicBase() ? musicTracks[target - musicBase()] : null;
    const cur = Main.curMusic;
    const mainAudible = main ? main.fade > MUSIC_AUDIBLE : cur <= 0 || Main.musicFade[cur] > MUSIC_AUDIBLE;
    const volume = Main.musicVolume;
    for (const t of musicTracks) {
        if (t === main) t.fade = Math.min(1, t.fade + MUSIC_FADE);
        else if (mainAudible) t.fade = Math.max(0, t.fade - MUSIC_FADE);
        const v = t.fade * volume;
        if (v !== t.sent) {
            bl.music.setVolume(t.id, v);
            t.sent = v;
        }
    }
}

// Os tres hooks rodam em todo quadro, ate na tela de carregamento, e nenhum
// espera o motor JS ocupado por outra thread (carga de mundo, save): sem a
// escolha, vale a do quadro anterior; sem o fade, o volume fica.
function hookMusic() {
    once('music', () => {
        musicHooked = true;
        const Main = Terraria.Main;
        const decide = (original, self) => {
            original(self);
            decideMusic();
        };
        Main['void UpdateAudio_DecideOnNewMusic()'].hook(decide, { ifBusy: 'skip' });
        Main['void UpdateAudio_DecideOnTOWMusic()'].hook(decide, { ifBusy: 'skip' });
        Main['void UpdateAudio()'].hook((original, self) => {
            original(self);
            stepModMusic();
        }, { ifBusy: 'original' });
    });
}

// ============================== ModContent ==============================
//
// Como o ModContent do tModLoader. O tipo e o modelo de um conteudo de mod:
//  - pela classe, o <T> de la: ModContent.ProjectileType(ExampleBobber);
//  - pelo nome: 'ExampleBobber', no mod de quem chama, ou no unico mod que
//    tem esse nome;
//  - por 'mod/Nome', com o id (ou uid) do manifesto: conteudo de outro mod.
// Tipo nao carrega nada: e so consultar o registro. Nao achou: 0, como la.
//
// E os arquivos do mod: ModContent.Request('Textures/brilho') carrega a
// textura na primeira vez e devolve a mesma depois (.Value e a Texture2D);
// ModContent.Texture(caminho) e o atalho para a Texture2D.

const textureAssets = new Map();   // arquivo -> Asset<Texture2D>

function contentRegistry(base) {
    if (base === ModItem) return itemsByType;
    if (base === ModProjectile) return projectilesByType;
    if (base === ModNPC) return npcsByType;
    if (base === ModBuff) return buffsByType;
    if (base === ModTile) return tilesByType;
    throw new TypeError('ModContent: espera ModItem, ModProjectile, ModNPC, ModBuff ou ModTile');
}

function findContent(byType, which) {
    if (typeof which === 'function') {
        const inst = contentByClass.get(which);
        return inst && byType.get(inst.Type) === inst ? inst : undefined;
    }
    const name = String(which);
    const slash = name.lastIndexOf('/');
    if (slash > 0) {
        const mod = findMod(name.slice(0, slash), 'ModContent');
        const want = name.slice(slash + 1);
        for (const inst of byType.values()) {
            if (mod && inst.Mod === mod && inst.constructor.name === want) return inst;
        }
        return undefined;
    }
    const caller = bl.mod;
    let found;
    let count = 0;
    for (const inst of byType.values()) {
        if (inst.constructor.name !== name) continue;
        if (caller && inst.Mod === caller) return inst;
        found = found || inst;
        count++;
    }
    if (count > 1) reportOnce('ModContent:' + name, "ModContent: '" + name + "' existe em " + count + " mods; peca por 'mod/" + name + "'");
    return count === 1 ? found : undefined;
}

const typeOfContent = (byType, which) => {
    const inst = findContent(byType, which);
    return inst ? inst.Type : 0;
};

// O arquivo de uma textura do mod, ou null. Aceita 'Textures/brilho.png',
// 'Textures/brilho' e 'Items/Espada' (dentro de Textures/, como o Texture do
// ModItem); e, como no tModLoader, 'mod/...' com o id de outro mod na frente.
function findModTexture(path) {
    const rel = String(path).replace(/^\/+/, '');
    const inRoot = (root, r) => {
        const file = /\.[a-z0-9]{2,4}$/i.test(r) ? r : r + '.png';
        for (const candidate of [file, 'Textures/' + file]) {
            const full = bl.path.join(root, candidate);
            if (bl.file.exists(full)) return full;
        }
        return null;
    };
    const own = bl.mod && bl.mod.path;
    const mine = own ? inRoot(own, rel) : null;
    if (mine) return mine;
    const slash = rel.indexOf('/');
    if (slash > 0) {
        const mod = findMod(rel.slice(0, slash), 'ModContent');
        if (mod && mod.path) return inRoot(mod.path, rel.slice(slash + 1));
    }
    return null;
}

const ModContent = Object.freeze({
    ItemType: (which) => typeOfContent(itemsByType, which),
    ProjectileType: (which) => typeOfContent(projectilesByType, which),
    NPCType: (which) => typeOfContent(npcsByType, which),
    BuffType: (which) => typeOfContent(buffsByType, which),
    TileType: (which) => typeOfContent(tilesByType, which),

    // O modelo da classe (o do register), ou undefined.
    GetInstance(cls) { return contentByClass.get(cls); },
    // Por nome, como o Find<T> do tModLoader: ModContent.Find(ModItem,
    // 'examplemod/ExampleItem'). Lanca se nao ha; o TryFind poe no Ref.
    Find(base, name) {
        const inst = findContent(contentRegistry(base), name);
        if (!inst) throw new Error("ModContent.Find: nao ha '" + name + "'");
        return inst;
    },
    TryFind(base, name, result) {
        const inst = findContent(contentRegistry(base), name);
        if (result && typeof result === 'object') result.value = inst;
        return inst !== undefined;
    },
    GetModItem: (type) => itemsByType.get(type),
    GetModProjectile: (type) => projectilesByType.get(type),
    GetModNPC: (type) => npcsByType.get(type),
    GetModBuff: (type) => buffsByType.get(type),
    GetModTile: (type) => tilesByType.get(type),

    // A textura do mod, carregada uma vez (so na thread do jogo, com o jogo
    // rodando: num hook, no SetStaticDefaults ou no PostSetupContent).
    Request(path) {
        const file = findModTexture(path);
        if (!file) throw new Error("ModContent.Request: nao achei a textura '" + path + "' na pasta do mod");
        let asset = textureAssets.get(file);
        if (!asset) {
            asset = bl.loadTextureAsset(file);
            textureAssets.set(file, asset);
        }
        return asset;
    },
    Texture(path) { return ModContent.Request(path).Value; },
    HasAsset: (path) => findModTexture(path) !== null,
    // O mesmo que new SoundStyle(caminho, opcoes), que ja guarda o som.
    SoundStyle: (path, options) => new SoundStyle(path, options),
});

globalThis.ModItem = ModItem;
globalThis.ModRecipe = ModRecipe;
globalThis.ModSystem = ModSystem;
globalThis.Mod = Mod;
globalThis.ModLoader = ModLoader;
globalThis.ModContent = ModContent;
globalThis.SoundStyle = SoundStyle;
globalThis.SoundEngine = SoundEngine;
globalThis.SoundLimitBehavior = SoundLimitBehavior;
globalThis.MusicLoader = MusicLoader;
globalThis.SceneEffectPriority = SceneEffectPriority;
globalThis.ModBuff = ModBuff;
globalThis.ModTile = ModTile;
globalThis.ModPlayer = ModPlayer;
globalThis.ModProjectile = ModProjectile;
globalThis.ModNPC = ModNPC;
globalThis.NPCShop = NPCShop;
globalThis.ModGore = ModGore;
globalThis.NPCHappiness = NPCHappiness;
globalThis.AffectionLevel = AffectionLevel;
globalThis.NPCLoot = NPCLoot;
globalThis.TooltipLine = TooltipLine;
globalThis.NPCSpawnInfo = NPCSpawnInfo;
globalThis.ModLocalization = ModLocalization;
})();
