import { ExampleCustomCurrency } from '../Items/ExampleItem.js';

const { DustID, NPCID, SoundID } = Terraria.ID;
const { BestiaryDatabaseNPCsPopulator, FlavorTextBestiaryInfoElement } = Terraria.GameContent.Bestiary;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];
const NewGore = Terraria.Gore['int NewGore(Vector2 Position, Vector2 Velocity, int Type, float Scale)'];

export class ExamplePerson extends ModNPC {
    constructor() {
        super();
        this.Texture = 'NPCs/ExamplePerson/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.npcFrameCount[this.Type] = 25;
        NPCID.Sets.ExtraFramesCount[this.Type] = 9;
        NPCID.Sets.AttackFrameCount[this.Type] = 4;
        NPCID.Sets.DangerDetectRange[this.Type] = 700;
        NPCID.Sets.AttackType[this.Type] = 1;
        NPCID.Sets.AttackTime[this.Type] = 60;
        NPCID.Sets.AttackAverageChance[this.Type] = 35;
        NPCID.Sets.HatOffsetY[this.Type] = 4;
        NPCID.Sets.ShimmerTownTransform[this.Type] = true;
        NPCID.Sets.NPCBestiaryDrawOffset.Add(this.Type, NPCID.Sets.NPCBestiaryDrawOffset.get_Item(NPCID.Guide));

        this.Happiness
            .SetNPCAffection(NPCID.Nurse, AffectionLevel.Love)
            .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
            .SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike)
            .SetBiomeAffection('Desert', AffectionLevel.Hate);
    }

    SetDefaults() {
        this.NPC.townNPC = true;
        this.NPC.friendly = true;
        this.NPC.width = 18;
        this.NPC.height = 40;
        this.NPC.aiStyle = 7;
        this.NPC.damage = 10;
        this.NPC.defense = 15;
        this.NPC.lifeMax = 250;
        this.NPC.HitSound = SoundID.NPCHit1;
        this.NPC.DeathSound = SoundID.NPCDeath1;
        this.NPC.knockBackResist = 0.5;
        this.AnimationType = NPCID.Guide;
    }

    SetBestiary(database, bestiaryEntry) {
        bestiaryEntry.Info.Add(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface);
        const flavor = FlavorTextBestiaryInfoElement.new();
        flavor._key = ModLocalization.Translate('Bestiary.ExamplePerson');
        bestiaryEntry.Info.Add(flavor);
    }

    SetNPCNameList() {
        return ['Someone', 'Somebody', 'Blocky', 'Colorless'];
    }

    HitEffect(npc, hitDirection, damage) {
        const dusts = npc.life > 0 ? 5 : 15;
        for (let k = 0; k < dusts; k++) {
            NewDust(npc.position, npc.width, npc.height, DustID.Blood, 0, 0, 0, Color.White, 1);
        }
        if (npc.life > 0 || Terraria.Main.netMode === 2) return;

        let variant = '';
        if (npc.IsShimmerVariant) variant += '_Shimmer';
        if (npc.altTexture === 1) variant += '_Party';
        const gore = (part) => ModGore.getTypeByName('ExamplePerson_Gore' + variant + '_' + part);
        const at = (dy) => Vector2.new(npc.position.X, npc.position.Y + dy);
        NewGore(at(0), npc.velocity, gore('Head'), 1);
        NewGore(at(20), npc.velocity, gore('Arm'), 1);
        NewGore(at(20), npc.velocity, gore('Arm'), 1);
        NewGore(at(34), npc.velocity, gore('Leg'), 1);
        NewGore(at(34), npc.velocity, gore('Leg'), 1);
    }

    CanTownNPCSpawn(numTownNPCs) {
        const exampleItem = ModItem.getTypeByName('ExampleItem');
        const players = Terraria.Main.player;
        for (let i = 0; i < players.length - 1; i++) {
            const player = players[i];
            if (player.active && player['bool HasItem(int type)'](exampleItem)) return true;
        }
        return false;
    }

    CheckConditions(left, right, top, bottom) {
        return bottom <= Terraria.Main.worldSurface;
    }

    SetChatButtons(npc, buttons) {
        buttons.button = Terraria.Localization.Language['string GetTextValue(string key)']('LegacyInterface.28');
    }

    OnChatButtonClicked(npc, firstButton) {
        if (firstButton) return 'Shop';
    }

    AddShops() {
        new NPCShop(this.Type, 'Shop')
            .Add(ModItem.getTypeByName('ExampleMeleeWeapon'))
            .Add(ModItem.getTypeByName('ExampleGun'))
            .Add(ModItem.getTypeByName('ExampleMagicWeapon'))
            .Add(ModItem.getTypeByName('ExampleYoyo'), { condition: () => !Terraria.Main.dayTime })
            .Add(ModItem.getTypeByName('ExampleSwingingEnergySword'), { currency: ExampleCustomCurrency.CurrencyId, price: 10 })
            .Register();
    }

    TownNPCAttackStrength(npc, attack) {
        attack.damage = 20;
        attack.knockback = 4;
    }

    TownNPCAttackProj(npc, attack) {
        attack.projType = ModProjectile.getTypeByName('ExampleAdvancedAnimatedProjectile');
    }

    TownNPCAttackProjSpeed(npc, attack) {
        attack.speed = 10;
    }

    GetChat(npc) {
        const keys = ['ExamplePerson_1', 'ExamplePerson_2'];
        return ModLocalization.GetTextValue('NPCChat.' + keys[Math.floor(Math.random() * keys.length)]);
    }
}
