const { BuffID, NPCID, SoundID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;
const { FlavorTextBestiaryInfoElement, MoonLordPortraitBackgroundProviderBestiaryInfoElement } = Terraria.GameContent.Bestiary;
const NewGore = Terraria.Gore['int NewGore(Vector2 Position, Vector2 Velocity, int Type, float Scale)'];

export class ExampleBoss extends ModNPC {
    constructor() {
        super();
        this.Texture = 'NPCs/ExampleBoss/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.npcFrameCount[this.Type] = 6;
        NPCID.Sets.MPAllowedEnemies[this.Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(this.Type);
    }

    SetDefaults() {
        this.NPC.width = 110;
        this.NPC.height = 110;
        this.NPC.aiStyle = -1;
        this.NPC.damage = 12;
        this.NPC.defense = 10;
        this.NPC.lifeMax = 2000;
        this.NPC.knockBackResist = 0;
        this.NPC.noGravity = true;
        this.NPC.noTileCollide = true;
        this.NPC.boss = true;
        this.NPC.npcSlots = 10;
        this.NPC.HitSound = SoundID.NPCHit1;
        this.NPC.DeathSound = SoundID.NPCDeath1;
        this.NPC.value = ModNPC.NPCValue(0, 3, 0, 0);
    }

    SetBestiary(database, bestiaryEntry) {
        bestiaryEntry.Info.Add(MoonLordPortraitBackgroundProviderBestiaryInfoElement.new());
        const flavor = FlavorTextBestiaryInfoElement.new();
        flavor._key = ModLocalization.Translate('Bestiary.ExampleBoss');
        bestiaryEntry.Info.Add(flavor);
    }

    ModifyNPCLoot(npcLoot) {
        npcLoot.Add(ItemDropRule.Common(ModItem.getTypeByName('ExampleItem'), 1, 15, 30));
    }

    HitEffect(npc, hitDirection, damage) {
        if (npc.life > 0 || Terraria.Main.netMode === 2) return;
        NewGore(npc.Right, Vector2.new(Math.random() * 5, 1 + Math.random() * 5), ModGore.getTypeByName('ExampleBoss_GoreFront'), 1.5);
        NewGore(npc.Left, Vector2.new(Math.random() * -5, 1 + Math.random() * 5), ModGore.getTypeByName('ExampleBoss_GoreBack'), 1.5);
    }

    PreAI(npc) {
        npc.buffImmune[BuffID.Confused] = true;
        return true;
    }

    AI(npc) {
        const players = Terraria.Main.player;
        if (npc.target < 0 || npc.target === 255 || players[npc.target].dead || !players[npc.target].active) {
            npc.TargetClosest(true);
        }
        const player = players[npc.target];

        if (player.dead || Terraria.Main.dayTime) {
            npc.velocity = Vector2.new(npc.velocity.X, npc.velocity.Y - 0.4);
            npc.EncourageDespawn(10);
        }

        const secondStage = npc.life <= npc.lifeMax * 0.5;
        if (secondStage && npc.ai[0] !== 1) npc.ai[0] = 1;

        const toPlayer = Vector2.Subtract(player.Center, npc.Center);
        let speed = 12, inertia = 80;
        if (npc.Top.Y > player.Bottom.Y) {
            speed += 4;
            inertia += 20;
        }
        if (secondStage) {
            speed *= 2;
            inertia *= 2;
        }
        const moveTo = Vector2.Multiply(Vector2.Normalize(toPlayer), speed);
        npc.velocity = Vector2.Divide(Vector2.Add(Vector2.Multiply(npc.velocity, inertia - 1), moveTo), inertia);
        npc.rotation = Math.atan2(toPlayer.Y, toPlayer.X) + Math.PI * 1.5;
    }

    FindFrame(npc, frameHeight) {
        const frame = npc.frame;
        let startFrame = 0;
        let finalFrame = 2;
        if (npc.ai[0] === 1) {
            startFrame = 3;
            finalFrame = Terraria.Main.npcFrameCount[this.Type] - 1;
            if (frame.Y < startFrame * frameHeight) frame.Y = startFrame * frameHeight;
        }
        npc.frameCounter += 0.5 + Vector2.Length(npc.velocity) / 10;
        if (npc.frameCounter > 5) {
            npc.frameCounter = 0;
            frame.Y += frameHeight;
            if (frame.Y > finalFrame * frameHeight) frame.Y = startFrame * frameHeight;
        }
        npc.frame = frame;
    }
}
