// A linha da vara de mod sai da ponta da vara (ModifyFishingLine), nao do
// centro do jogador. Poe a ExampleFishingRod no slot 0, o ExampleYoyo no 1 e
// o Code 1 do jogo no 2 (o logo da One Drop, para comparar),
// lanca a boia e confere que a linha comeca em (43, -30) do centro, o
// lineOriginOffset da vara. O logo do ioio e visual (print do tooltip).
const Main = Terraria.Main;

function byName(vanilla, isMod, getMod) {
    const out = {};
    for (let t = vanilla; isMod(t); t++) {
        const m = getMod(t);
        if (m) out[m.constructor.name] = t;
    }
    return out;
}

let forceUse = false, logged = false;
Terraria.Player['void ItemCheckWrapped(int i)'].hook((original, self, i) => {
    if (forceUse && i === Main.myPlayer) self.controlUseItem = true;
    original(self, i);
});

let offset = null;
Terraria.Main['void DrawProj_FishingLine(Projectile proj, Player theOwner, ref float polePosX, ref float polePosY, Vector2 mountedCenter)'].hook(
    (original, proj, owner, px, py, center) => {
        original();
        if (logged) return;
        logged = true;
        offset = { X: Math.round(px.value - owner.Center.X), Y: Math.round(py.value - owner.Center.Y - owner.gfxOffY) };
        bl.log('fishline ponta da linha: ' + offset.X + ', ' + offset.Y + ' do centro (direcao ' + owner.direction + ')');
    });

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    if (frames === 60) {
        const items = byName(bl.items.vanillaCount, bl.items.isModItem, ModItem.getModItem);
        self.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](items.ExampleFishingRod, null);
        self.inventory[1]['void SetDefaults(int Type, ItemVariant variant)'](items.ExampleYoyo, null);
        self.inventory[2]['void SetDefaults(int Type, ItemVariant variant)'](Terraria.ID.ItemID.Code1, null);
        self.selectedItemState.selected = 0;
        self.direction = 1;
        forceUse = true;
    }
    if (frames === 70) forceUse = false;
    if (frames === 200) {
        const ok = offset && offset.X === 43 && offset.Y === -30;
        bl.log('fishline FIM: ' + (ok ? 'tudo ok' : 'FALHOU (' + (offset ? offset.X + ', ' + offset.Y : 'nenhuma linha') + ')'));
    }
});
bl.log('fishline: carregado');
