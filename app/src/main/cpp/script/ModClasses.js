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
const readyTasks = [];
let readyHooked = false;
function whenReady(task) {
    readyTasks.push(task);
    if (readyHooked) return;
    readyHooked = true;
    bl.onContentReady(() => {
        for (const t of readyTasks) guard('PostSetupContent', t);
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
    // Uma vez, quando as receitas do jogo ja existem: this.CreateRecipe(...).
    AddRecipes() {}

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
    UpdateAccessory(item, player, hideVisual) {}
    // No inventario, todo quadro.
    UpdateInventory(item, player) {}
    // No chao: a cor com que o item e desenhado. Devolva uma Color (ou nada,
    // para a do jogo). `item` e o WorldItem; a luz do lugar chega em lightColor.
    GetAlpha(item, lightColor) { return undefined; }

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

    // Bola de golfe: tee, taco e o projetil `projType`, como as do jogo.
    DefaultToGolfBall(projType) {
        this.Item['void DefaultToGolfBall(int projid)'](projType);
    }

    // Receita que da este item: this.CreateRecipe(stack).AddIngredient(...).AddTile(...).Register().
    CreateRecipe(stack = 1) {
        return new ModRecipe().SetResult(this.Type, stack);
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
        defineEntityField(Terraria.Item, 'ModItem');
        const inst = new cls();
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
            },
        });
        inst.Type = type;
        itemsByType.set(type, inst);

        setupTooltip(inst, name, type);
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
                    const stats = { position: { X: x, Y: y }, velocity: { X: sx, Y: sy }, type, damage, knockBack };
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
                if (item.accessory) guard(m.constructor.name + '.UpdateAccessory', () => m.UpdateAccessory(item, self, false));
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

/**
 * Uma receita do jogo, como o ModRecipe do ExMod. So vale no AddRecipes (ou
 * em bl.onContentReady): antes disso o jogo nao montou as receitas dele.
 */
class ModRecipe {
    static MaxIngredients = 15;

    constructor() {
        this.recipe = Terraria.Recipe.currentRecipe;
        this.ingredients = 0;
    }

    SetResult(type, stack = 1) {
        const it = this.recipe.createItem;
        it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        it.stack = Math.max(1, stack | 0);
        return this;
    }

    AddIngredient(type, stack = 1) {
        if (this.ingredients >= ModRecipe.MaxIngredients) {
            bl.log('ModRecipe: mais de ' + ModRecipe.MaxIngredients + ' ingredientes; ' + type + ' ficou de fora');
            return this;
        }
        const it = this.recipe.requiredItem[this.ingredients++];
        it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        it.stack = Math.max(1, stack | 0);
        Terraria.ID.ItemID.Sets.IsAMaterial[type] = true;
        return this;
    }

    AddTile(tileType) {
        this.recipe['void SetCraftingStation(int tileType)'](tileType);
        return this;
    }

    // needWater, needLava, needHoney, needSnowBiome, needGraveyardBiome, alchemy...
    SetProperty(name, value) {
        this.recipe[name] = value;
        return this;
    }

    Register() {
        if (Terraria.Recipe.numRecipes >= Terraria.Main.recipe.length) {
            bl.log('ModRecipe: limite de receitas do jogo (' + Terraria.Main.recipe.length + ') atingido');
            return;
        }
        Terraria.Recipe['void AddRecipe()']();
        recipesAdded++;
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
    bl.log('receitas de mod: ' + recipesAdded);
}

// ============================== ModProjectile ==============================

const projectilesByType = new Map();
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

    // IA: false no PreAI pula a IA do jogo (a do aiStyle) e o AI.
    PreAI(proj) { return true; }
    AI(proj) {}
    PostAI(proj) {}
    // Morte: false no PreKill tira os efeitos do jogo (poeira, som); morre igual.
    PreKill(proj, timeLeft) { return true; }
    OnKill(proj, timeLeft) {}
    // Acertou um NPC / um jogador.
    OnHitNPC(proj, npc) {}
    OnHitPlayer(proj, player) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModProjectile)) {
            throw new TypeError('ModProjectile.register(Classe): passe a classe, que estende ModProjectile');
        }
        defineEntityField(Terraria.Projectile, 'ModProjectile');
        const inst = new cls();
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

    if (has('PreAI') || has('AI') || has('PostAI')) once('proj.AI', () => {
        Pr['void AI()'].hook((original, p) => {
            const m = projectileOf(p);
            if (!m) return original(p);
            const n = m.constructor.name;
            if (guard(n + '.PreAI', () => m.PreAI(p)) !== false) {
                original(p);
                guard(n + '.AI', () => m.AI(p));
            }
            guard(n + '.PostAI', () => m.PostAI(p));
        }, self);
    });

    if (has('PreKill') || has('OnKill')) once('proj.Kill', () => {
        Pr['void Kill()'].hook((original, p) => {
            const m = projectileOf(p);
            if (!m || !p.active) return original(p);
            const n = m.constructor.name;
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
    // Anima como este NPC do jogo (0 = nao anima). Pode vir do SetDefaults.
    AnimationType = 0;
    // Texto, ou { 'pt-BR': ..., 'en-US': ... }. Vazio: NPCName.<Classe> em
    // Localization/*.json, e sem isso o nome da classe.
    DisplayName = '';
    // Relativo a Textures/, sem .png. Padrao: o nome da classe.
    Texture = this.constructor.name;
    // true: sem entrada no Bestiario.
    HideFromBestiary = false;

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

    static NPCValue(p = 0, g = 0, s = 0, c = 0) {
        return p * 1000000 + g * 10000 + s * 100 + c;
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModNPC)) {
            throw new TypeError('ModNPC.register(Classe): passe a classe, que estende ModNPC');
        }
        defineEntityField(Terraria.NPC, 'ModNPC');
        const inst = new cls();
        const name = cls.name;
        let animation = inst.AnimationType | 0;
        let type = -1;
        let townNpc = false;
        const def = {
            name,
            texture: texturePath(inst.Texture || name),
            animationType: animation,
            displayName: inst.DisplayName || localized('NPCName', name) || name,
            setDefaults(npc) {
                const m = bindInstance(inst.Clone(npc), npc, 'ModNPC');
                m.SetDefaults(npc);
                m.ApplyBuffImmunity(npc);
                m.PostSetDefaults(npc);
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
        if (overrides(cls, ModNPC, 'SpawnChance')) spawnable.push(inst);
        whenReady(() => {
            if (!inst.HideFromBestiary) guard(name + '.SetBestiary', () => registerBestiary(inst, townNpc));
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

function hookNpc(cls) {
    const N = Terraria.NPC;
    const has = (name) => overrides(cls, ModNPC, name);
    const self = { minType: FIRST_NPC };

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

globalThis.ModItem = ModItem;
globalThis.ModRecipe = ModRecipe;
globalThis.ModProjectile = ModProjectile;
globalThis.ModNPC = ModNPC;
globalThis.NPCLoot = NPCLoot;
globalThis.NPCSpawnInfo = NPCSpawnInfo;
globalThis.ModLocalization = ModLocalization;
})();
