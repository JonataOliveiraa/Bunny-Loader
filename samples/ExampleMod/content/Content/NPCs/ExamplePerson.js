const { DustID, NPCID, SoundID } = Terraria.ID;
const { BestiaryDatabaseNPCsPopulator, FlavorTextBestiaryInfoElement } = Terraria.GameContent.Bestiary;
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

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

    GetChat(npc) {
        const keys = ['ExamplePerson_1', 'ExamplePerson_2'];
        return ModLocalization.GetTextValue('NPCChat.' + keys[Math.floor(Math.random() * keys.length)]);
    }
}
