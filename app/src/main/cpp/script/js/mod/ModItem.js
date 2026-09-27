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
    UpdateInventory(item, player) {}
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
                const m = Entities.Bind(inst.Clone(item), item, 'ModItem');
                m.SetDefaults(item);
                m.PostSetDefaults(item);
            },
            setStaticDefaults() {
                inst.SetStaticDefaults();
                inst.PostStaticDefaults();
                Templates.HideFromMenu('item', inst, inst.Type, () => Terraria.ID.ItemID.Sets.Deprecated[inst.Type]);
            },
        });
        inst.Type = type;
        ItemLoader.ByType.set(type, inst);
        ItemLoader.SetupTooltip(inst, name, type);

        Ready.Add(() => inst.AddRecipeGroups(), 'groups');
        Ready.Add(() => {
            inst.AddRecipes();
            inst.PostSetupContent();
        });

        ItemLoader.Hook(cls);
        Hooks.Once('item.Clone', ItemLoader.HookClone);
        return type;
    }

    static isModType(type) { return bl.items.isModItem(type); }
    static isModItem(item) { return !!item && bl.items.isModItem(item.type); }
    static getTypeByName(name) { return bl.items.typeOf(name); }
    static getModItem(type) { return ItemLoader.ByType.get(type); }
    static getByName(name) { return ItemLoader.ByType.get(bl.items.typeOf(name)); }
}
