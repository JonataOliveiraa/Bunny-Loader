const { SoundID, NPCID, ItemID, DustID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

const Common = ItemDropRule['IItemDropRule Common(int itemId, int chanceDenominator, int minimumDropped, int maximumDropped)'];
const NormalvsExpert = ItemDropRule['IItemDropRule NormalvsExpert(int itemId, int chanceDenominatorInNormal, int chanceDenominatorInExpert)'];
const NewDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export class ExampleSlimeNPC extends ModNPC {
    constructor() {
        super();
        this.Texture = 'NPCs/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.npcFrameCount[this.Type] = 2;
        Terraria.Main.slimeRainNPC[this.Type] = true;
        NPCID.Sets.ShimmerTransformToNPC[this.Type] = NPCID.ShimmerSlime;
    }

    SetDefaults() {
        this.NPC.aiStyle = 1;
        this.NPC.damage = 7;
        this.NPC.defense = 2;
        this.NPC.lifeMax = 25;
        this.NPC.alpha = 175;
        const color = this.NPC.color;
        color.R = 40; color.G = 200; color.B = 255; color.A = 100;
        this.NPC.HitSound = SoundID.NPCHit1;
        this.NPC.DeathSound = SoundID.NPCDeath1;
        this.NPC.value = ModNPC.NPCValue(0, 0, 0, 25);

        this.AnimationType = NPCID.BlueSlime;
    }

    ApplyBuffImmunity(npc) {
        npc.buffImmune[20] = true;
    }

    ModifyNPCLoot(npcLoot) {
        npcLoot.Add(Common(ItemID.Gel, 1, 1, 2));
        npcLoot.Add(Common(ModItem.getTypeByName('ExampleItem'), 3, 5, 10));
        npcLoot.Add(NormalvsExpert(ItemID.SlimeStaff, 10000, 7000));
    }

    HitEffect(npc, hitDirection, damage) {
        const dead = npc.life <= 0;
        for (let i = 0; i < (dead ? 45 : 10); i++) {
            const speedX = dead ? (Math.random() - 0.5) * 2 * hitDirection : hitDirection * 0.3;
            const scale = dead ? 1 + (Math.random() - 0.5) : 0.6 + (Math.random() - 0.5);
            NewDust(npc.position, npc.width, npc.height, DustID.TintableDust, speedX,
                    (Math.random() - 0.5) * 2, 50 + Math.floor(Math.random() * 50), npc.color, scale);
        }
    }
}
