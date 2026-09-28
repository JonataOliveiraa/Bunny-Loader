// Cada Item de mod ganha a própria cópia do molde em item.ModItem; o
// SetDefaults roda nela.
class ModItem {
    static CommonMaxStack = 9999;

    Type = undefined;
    HideFromModMenu = false;
    DisplayName = '';
    Tooltip = '';
    TooltipLines = [];
    Texture = this.constructor.name;

    get Item() { return Entities.Of(this); }

    Clone(newItem) { return Entities.Clone(this); }

    // No registro, com o tipo já dado (o Load do tModLoader: AddEquipTexture...).
    Load() {}
    SetStaticDefaults() {}
    SetDefaults(item) {}
    PostStaticDefaults() {}
    PostSetDefaults(item) {}
    PostSetupContent() {}
    ModifyTooltipLines() {}
    ModifyTooltips(item, tooltips) {}
    AddRecipeGroups() {}
    AddRecipes() {}
    OnCraft(item, player, recipe) {}

    CanUseItem(item, player) { return true; }
    UseItem(item, player) {}
    HoldItem(item, player) {}
    UseStyle(item, player, mountOffset, heldItemFrame) {}
    HoldStyle(item, player, mountOffset, heldItemFrame) {}
    HoldoutOffset(item, player) { return undefined; }
    CanShoot(item, player) { return true; }
    ModifyShootStats(item, player, stats) {}
    Shoot(item, player, position, velocity, type, damage, knockBack) { return true; }
    OnHitNPC(item, player, npc, damageDone, knockBack, crit) {}
    UpdateEquip(item, player) {}
    UpdateAccessory(item, player, vanity, hideVisual) {}
    // false recusa o par (arrastar para o slot e a troca pelo toque, que vai
    // para o slot do que recusou). Chamado nos dois itens do par.
    CanAccessoryBeEquippedWith(equippedItem, incomingItem, player) { return true; }
    // Acessório no slot de vaidade.
    UpdateVanity(item, player) {}
    // O conjunto do 1.4.5 (tooltip "Bônus definido" e efeito pelo jogo): no
    // AddArmorSets, CreateArmorSet(cabeça, corpo, pernas, texto) com os tipos;
    // 0 é "qualquer". O texto é uma chave de tradução ou o próprio texto. O
    // efeito é o UpdateArmorSet de cada peça de mod vestida.
    AddArmorSets() {}
    CreateArmorSet(head, body, legs, text = 'ArmorSetBonus.Empty', primaryPart = 0) {
        ArmorSetLoader.CreateArmorSet(head, body, legs, text, primaryPart);
    }
    CreateArmorSets(heads = [0], bodies = [0], legs = [0], text = 'ArmorSetBonus.Empty', primaryPart = 0) {
        for (const h of heads) for (const b of bodies) for (const l of legs) ArmorSetLoader.CreateArmorSet(h, b, l, text, primaryPart);
    }
    // O jeito do tModLoader, sem o tooltip do 1.4.5: chamados para cada peça
    // vestida que é de mod. Com o CreateArmorSet, não sobrescreva o IsArmorSet
    // (o efeito rodaria duas vezes).
    IsArmorSet(head, body, legs) { return false; }
    UpdateArmorSet(item, player) {}

    // Conjunto de vaidade: head, body e legs são os SLOTS desenhados
    // (player.head...). Sem sobrescrever, é o IsArmorSet dos itens desses slots.
    IsVanitySet(head, body, legs) {
        const sample = (table, slot) => ItemLoader.Sample(slot > 0 && slot < table.length ? table[slot] : 0);
        return this.IsArmorSet(sample(Terraria.Item.headType, head), sample(Terraria.Item.bodyType, body),
                               sample(Terraria.Item.legType, legs));
    }
    PreUpdateVanitySet(player) {}
    UpdateVanitySet(player) {}
    ArmorSetShadows(player) {}
    // O slot que o jogo desenha para esta peça (equipSlot e robes são Ref):
    // o manto põe robes = true e as pernas dele em equipSlot.
    SetMatch(male, equipSlot, robes) {}
    // Cada quadro, com esta textura vestida (type: o EquipType).
    EquipFrameEffects(player, type) {}

    // As asas deste item: os `ref` do jogo chegam como Ref (.value).
    VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend) {}
    HorizontalWingSpeeds(item, player, speed, acceleration) {}
    // true: o mod anima as asas (o WingFrame do jogo não roda).
    WingUpdate(player, inUse) { return false; }

    // O WingStats das asas deste item (no SetStaticDefaults), como o do ExMod do TL.
    SetWingStats(flyTime = 100, flySpeedOverride = -1, accelerationMultiplier = 1, hasHoldDownHoverFeatures = false,
                 hoverFlySpeedOverride = -1, hoverAccelerationMultiplier = 1) {
        const slot = this.Item ? this.Item.wingSlot : -1;
        if (!(slot > 0)) throw new Error(this.constructor.name + '.SetWingStats: o item nao tem asas (' + this.Texture + '_Wings.png)');

        const stats = Terraria.ID.ArmorIDs.Wing.Sets.Stats[slot];
        stats.FlyTime = flyTime;
        stats.AccRunSpeedOverride = flySpeedOverride;
        stats.AccRunAccelerationMult = accelerationMultiplier;
        stats.HasDownHoverStats = hasHoldDownHoverFeatures;
        stats.DownHoverSpeedOverride = hoverFlySpeedOverride;
        stats.DownHoverAccelerationMult = hoverAccelerationMultiplier;
    }
    UpdateInventory(item, player) {}

    // Prefixos: que categorias este item pega (arma com dano, sem ser munição
    // nem consumível; o padrão vem de melee/ranged/magic/summon, como no tModLoader).
    MeleePrefix(item = this.Item) { return !!item.melee && !item.noUseGraphic; }
    WeaponPrefix(item = this.Item) { return !!item.melee && !!item.noUseGraphic; }
    RangedPrefix(item = this.Item) { return !!item.ranged; }
    MagicPrefix(item = this.Item) { return !!item.magic; }
    SummonPrefix(item = this.Item) { return !!item.summon; }
    // Um prefixo forçado ao rolar (> 0), ou -1.
    ChoosePrefix(item, rand) { return -1; }
    // true força um prefixo, false impede; null = o do jogo (pre: -1 criar, -2 reforja).
    PrefixChance(item, pre, rand) { return null; }
    AllowPrefix(item, pre) { return true; }
    // Depois dos status do prefixo (e do Apply do ModPrefix).
    ApplyPrefix(item, pre) {}

    GetAlpha(item, lightColor) { return undefined; }
    // ModifyFishingLine(item, bobber, line) também vale (line.lineOriginOffset, line.lineColor).
    ModifyFishingLine(item, bobber, lineOriginOffset, lineColor) {}

    CloneDefaults(type) {
        const source = Terraria.Item.new();
        source['void .ctor()']();
        source['void SetDefaults(int Type, ItemVariant variant)'](type, null);

        for (const key of ItemLoader.CLONED_FIELDS) {
            try {
                this.Item[key] = source[key];
            } catch (e) {
                // campo que esta versão não tem
            }
        }
    }

    SetDefaultWeaponStyle(useTime = 30, autoReuse = false) {
        const { ItemUseStyleID } = Terraria.ID;
        const item = this.Item;

        item.useTime = useTime;
        item.useAnimation = useTime;
        item.autoReuse = autoReuse;

        if (item.melee) item.useStyle = ItemUseStyleID.Swing;
        else if (item.shoot > 0 && !item.consumable) item.useStyle = ItemUseStyleID.Shoot;
        else item.useStyle = ItemUseStyleID.Swing;
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
        const item = this.Item;
        item.createTile = typeToPlace;
        item.placeStyle = styleToPlace;
        item.useStyle = Terraria.ID.ItemUseStyleID.Swing;
        item.useAnimation = 15;
        item.useTime = 10;
        item.maxStack = ModItem.CommonMaxStack;
        item.useTurn = true;
        item.autoReuse = true;
        item.consumable = true;
    }

    // A tocha do jogo (segurar, luz, colocar na parede), colocando o tile de mod.
    DefaultToTorch(tileType, styleToPlace = 0, allowWaterPlacement = false) {
        this.Item['void DefaultToTorch(int tileStyleToPlace, bool allowWaterPlacement)'](styleToPlace, allowWaterPlacement);
        this.Item.createTile = tileType;
    }

    // A caixa de música do jogo, colocando o tile de mod (MusicLoader.AddMusicBox).
    DefaultToMusicBox(tileType, styleToPlace = 0) {
        this.Item['void DefaultToMusicBox(int style)'](styleToPlace);
        this.Item.createTile = tileType;
        this.Item.placeStyle = styleToPlace;
    }

    DefaultToFood(buffType, buffTime, useGulpSound = false, animationTime = 17) {
        const { SoundID } = Terraria.ID;
        const item = this.Item;
        item.useStyle = useGulpSound ? 9 : 2;
        item.UseSound = useGulpSound ? SoundID.Item3 : SoundID.Item2;
        item.useTurn = true;
        item.useTime = item.useAnimation = animationTime;
        item.maxStack = ModItem.CommonMaxStack;
        item.consumable = true;
        item.buffType = buffType;
        item.buffTime = buffTime;
        item.rare = 1;
        item.value = Terraria.Item.buyPrice(0, 0, 20, 0);
    }

    // Quadros do item numa tira vertical, no inventário, no chão e na mão.
    SetItemAnimation(frameCount, ticksPerFrame = 5, pingPong = false) {
        const animation = Terraria.DataStructures.DrawAnimationVertical.new();
        animation['void .ctor(int ticksperframe, int frameCount, bool pingPong)'](ticksPerFrame, frameCount, pingPong);
        ItemLoader.RegisterAnimation(this.Type, animation);
        return animation;
    }

    DefaultToWhip(projType, damage, knockBack, shootSpeed, animationTime = 30) {
        this.Item['void DefaultToWhip(int projectileId, int dmg, float kb, float shootspeed, int animationTotalTime)'](
            projType, damage, knockBack, shootSpeed, animationTime);
    }

    DefaultToSpear(projType, pushForwardSpeed, animationTime) {
        this.Item['void DefaultToSpear(int projType, float pushForwardSpeed, int animationTime)'](
            projType, pushForwardSpeed, animationTime);
    }

    DefaultToGolfBall(projType) {
        this.Item['void DefaultToGolfBall(int projid)'](projType);
    }

    CreateRecipe(stack = 1) {
        return new ModRecipe().SetResult(this.Type, stack);
    }

    // Grupo com o nome do primeiro item ("Qualquer <nome>").
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

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModItem)) {
            throw new TypeError('ModItem.register(Classe): passe a classe, que estende ModItem');
        }

        GoreLoader.Autoload();
        Entities.Define(Terraria.Item, 'ModItem');

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        const type = bl.items.register({
            name,
            texture: ModFiles.Texture(inst.Texture),
            displayName: inst.DisplayName || Lang.Localized('ItemName', name) || name,
            setDefaults(item) {
                EquipLoader.Install();
                EquipLoader.Apply(item, inst.Type);
                // O jogo faz isso no SetDefaults dele, que o item de mod não
                // chama; sem o material, o Guia não o aceita.
                item.material = Terraria.ID.ItemID.Sets.IsAMaterial[inst.Type];

                const m = Entities.Bind(inst.Clone(item), item, 'ModItem');
                m.SetDefaults(item);
                m.PostSetDefaults(item);
            },
            setStaticDefaults() {
                EquipLoader.Install();

                // O this.Item do tModLoader no SetStaticDefaults (Item.wingSlot...): a amostra do jogo.
                inst.__entity = ItemLoader.SampleAddress(inst.Type);
                try {
                    inst.SetStaticDefaults();
                    inst.PostStaticDefaults();
                } finally {
                    inst.__entity = 0;
                }
                Templates.HideFromMenu('item', inst, inst.Type, () => Terraria.ID.ItemID.Sets.Deprecated[inst.Type]);
            },
        });
        inst.Type = type;
        Lang.Follow('ItemName.' + name, inst.DisplayName || Lang.Localized('ItemName', name));
        ItemLoader.ByType.set(type, inst);
        EquipLoader.Autoload(inst, cls, name);
        Safe.Run(name + '.Load', () => inst.Load());
        ItemLoader.SetupTooltip(inst, name, type);

        Ready.Add(() => inst.AddRecipeGroups(), 'groups');
        Ready.Add(() => {
            Safe.Run(name + '.AddArmorSets', () => inst.AddArmorSets());
            inst.AddRecipes();
            inst.PostSetupContent();
        });

        ItemLoader.Hook(cls);
        Hooks.Once('item.Clone', ItemLoader.HookClone);
        PrefixLoader.WantRollable();
        return type;
    }

    static isModType(type) { return bl.items.isModItem(type); }
    static isModItem(item) { return !!item && bl.items.isModItem(item.type); }
    static getTypeByName(name) { return bl.items.typeOf(name); }
    static getModItem(type) { return ItemLoader.ByType.get(type); }
    static getByName(name) { return ItemLoader.ByType.get(bl.items.typeOf(name)); }
}
