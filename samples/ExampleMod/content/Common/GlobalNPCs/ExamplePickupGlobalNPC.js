const { NPCID } = Terraria.ID;
const Main = Terraria.Main;
const NewItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];

// A queda do ExamplePickup, nas mesmas condições dos corações e das
// estrelas (o NPCLoot_DropCommonLifeAndMana do jogo): sorte de 1 em 6, só de
// inimigo que machuca, nunca dos slimes que se dividem e só para quem ainda
// não tem o buff.
export class ExamplePickupGlobalNPC extends GlobalNPC {
    static Splitting = [NPCID.MotherSlime, NPCID.CorruptSlime, NPCID.Slimer];

    // No servidor (ou sozinho): o item que cai vai para todos.
    OnKill(npc) {
        if (ExamplePickupGlobalNPC.Splitting.includes(npc.netID) || npc.lifeMax <= 1 || npc.damage <= 0) return;

        const closest = Main.player[Terraria.Player['byte FindClosest(Vector2 Position, int Width, int Height)'](npc.position, npc.width, npc.height)];
        const hasBuff = closest.FindBuffIndex(ModContent.BuffType('ExampleDefenseBuff')) >= 0;
        if (closest.RollLuck(6) !== 0 || !Rand.NextBool(2) || hasBuff) return;

        NewItem(npc.GetItemSource_Loot(), Math.floor(npc.position.X), Math.floor(npc.position.Y), npc.width, npc.height,
            ModContent.ItemType('ExamplePickup'), 1, false, 0, 0, null, null);
    }
}
