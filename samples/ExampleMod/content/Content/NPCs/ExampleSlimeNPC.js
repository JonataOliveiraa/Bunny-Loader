const { SoundID, NPCID, ItemID, DustID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

const dropRule = ItemDropRule['IItemDropRule Common(int itemId, int chanceDenominator, int minimumDropped, int maximumDropped)'];
const newDust = Terraria.Dust['int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)'];

export const ExampleSlimeNPC = bl.npcs.register({
    name: 'ExampleSlimeNPC',
    texture: 'Textures/NPCs/ExampleSlimeNPC.png',
    frames: 2,
    animationType: NPCID.BlueSlime,
    displayName: { 'pt-BR': 'Slime de Exemplo', 'en-US': 'Example Slime' },
    setStaticDefaults(type) {
        Terraria.Main.slimeRainNPC[type] = true;
        NPCID.Sets.ShimmerTransformToNPC[type] = NPCID.ShimmerSlime;
        const drops = Terraria.Main.ItemDropsDB;
        const add = (rule) => drops['IItemDropRule RegisterToNPC(int type, IItemDropRule entry)'](type, rule);
        add(dropRule(ItemID.Gel, 1, 1, 2));
        add(dropRule(ModItem.getTypeByName('ExampleItem'), 3, 5, 10));
        add(ItemDropRule['IItemDropRule NormalvsExpert(int itemId, int chanceDenominatorInNormal, int chanceDenominatorInExpert)'](
            ItemID.SlimeStaff, 10000, 7000));
    },
    setDefaults(npc) {
        npc.aiStyle = 1;
        npc.damage = 7;
        npc.defense = 2;
        npc.lifeMax = 25;
        npc.alpha = 175;
        const color = npc.color;
        color.R = 40; color.G = 200; color.B = 255; color.A = 100;
        npc.HitSound = SoundID.NPCHit1;
        npc.DeathSound = SoundID.NPCDeath1;
        npc.value = 25;                  // 25 cobres
        npc.buffImmune[20] = true;       // veneno
    },
    // Gosma da cor do slime a cada acerto; na morte, muito mais.
    hitEffect(npc, hitDirection) {
        const dead = npc.life <= 0;
        for (let i = 0; i < (dead ? 45 : 10); i++) {
            const speedX = dead ? (Math.random() - 0.5) * 2 * hitDirection : hitDirection * 0.3;
            const scale = dead ? 1 + (Math.random() - 0.5) : 0.6 + (Math.random() - 0.5);
            newDust(npc.position, npc.width, npc.height, DustID.TintableDust, speedX,
                    (Math.random() - 0.5) * 2, 50 + Math.floor(Math.random() * 50), npc.color, scale);
        }
    },
});
