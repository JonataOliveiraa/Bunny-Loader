// Castelo de pedra com duas torres, construído em volta do jogador.
// Rodar no Editor (Mod Menu), de preferência num mundo de um jogador: no
// multijogador o que o cliente muda aqui não vai para o servidor.
const W = Terraria.WorldGen, { TileID: T, WallID: WL } = ID;
const place = (x, y, type, style = 0) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, style);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const wall = (x, y, type) => W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, type, true);
const unwall = (x, y) => W['void KillWall(int i, int j, bool fail)'](x, y, false);
const rect = (x0, y0, x1, y1, fn) => { for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++) fn(x, y); };
const brick = (x, y) => place(x, y, T.GrayBrick);

// Medidas: o chão é a linha sob os pés; torres de 7 de largura nas pontas.
const cx = Math.floor((player.position.X + player.width / 2) / 16);
const G = Math.floor((player.position.Y + player.height) / 16);
const L = cx - 22, R = cx + 22, TL = L + 6, TR = R - 6;
const KEEP_TOP = G - 16, TOWER_TOP = G - 24;

// 1. Terreno limpo e fundação
rect(L - 2, TOWER_TOP - 2, R + 2, G - 1, (x, y) => { kill(x, y); unwall(x, y); });
rect(L - 2, G, R + 2, G + 3, (x, y) => { kill(x, y); brick(x, y); });

// 2. Paredes de fundo, com vitrais
const glassKeep = (x, y) => (x - TL) % 6 >= 2 && (x - TL) % 6 <= 3 && ((y >= G - 14 && y <= G - 11) || (y >= G - 6 && y <= G - 4));
const glassTower = (x, y) => x % 7 === (L + 3) % 7 && [G - 5, G - 4, G - 13, G - 12, G - 21, G - 20].includes(y);
rect(TL + 1, KEEP_TOP + 1, TR - 1, G - 1, (x, y) => wall(x, y, glassKeep(x, y) ? WL.Glass : WL.GrayBrick));
for (const x0 of [L, TR]) rect(x0 + 1, TOWER_TOP + 1, x0 + 5, G - 1, (x, y) => wall(x, y, glassTower(x, y) ? WL.Glass : WL.GrayBrick));

// 3. Estrutura: colunas, tetos, pisos
for (let y = TOWER_TOP; y < G; y++) { brick(L, y); brick(TL, y); brick(TR, y); brick(R, y); }
for (let x = L; x <= R; x++) if (x <= TL || x >= TR) brick(x, TOWER_TOP);
for (let x = TL; x <= TR; x++) brick(x, KEEP_TOP);
for (let x = TL + 1; x < TR; x++) place(x, G - 8, Math.abs(x - cx) <= 2 ? T.Platforms : T.GrayBrick);
for (const y of [G - 8, G - 16]) for (let k = 1; k <= 5; k++) { place(L + k, y, T.Platforms); place(TR + k, y, T.Platforms); }
for (const x of [L + 3, TR + 3]) { kill(x, TOWER_TOP); place(x, TOWER_TOP, T.Platforms); } // alçapão para o topo

// 4. Ameias
for (let x = L; x <= TL; x += 2) brick(x, TOWER_TOP - 1);
for (let x = TR; x <= R; x += 2) brick(x, TOWER_TOP - 1);
for (let x = TL + 2; x <= TR - 2; x += 3) brick(x, KEEP_TOP - 1);

// 5. Portas: entrada dos dois lados e passagem torre <-> salão em cada andar
const door = (x, floorY) => { for (let y = floorY - 3; y < floorY; y++) kill(x, y); place(x, floorY - 2, T.ClosedDoor); };
door(L, G); door(R, G);
for (const floorY of [G, G - 8, G - 16]) { door(TL, floorY); door(TR, floorY); }

// 6. Mobília
place(cx - 9, G - 1, T.Tables); place(cx - 11, G - 1, T.Chairs); place(cx - 7, G - 1, T.Chairs);
place(cx + 6, G - 1, T.Containers);
place(cx + 12, G - 1, T.Bookcases);
place(cx - 3, G - 1, T.Lamps); place(cx + 3, G - 1, T.Lamps);
place(cx - 7, G - 7, T.Chandeliers); place(cx + 7, G - 7, T.Chandeliers);
place(cx - 8, G - 9, T.Pianos); place(cx + 9, G - 9, T.Beds); place(cx + 13, G - 9, T.Bookcases);
place(cx, KEEP_TOP + 1, T.Chandeliers);
[TL + 2, TL + 5, TR - 5, TR - 2].forEach((x, i) => place(x, KEEP_TOP + 1, T.Banners, i % 4));
for (const x0 of [L, TR]) {
    place(x0 + 3, TOWER_TOP + 1, T.Banners, x0 === L ? 0 : 2);
    for (const y of [G - 4, G - 12, G - 20]) { place(x0 + 1, y, T.Torches); place(x0 + 5, y, T.Torches); }
}
for (const y of [G - 4, G - 12]) { place(TL + 1, y, T.Torches); place(TR - 1, y, T.Torches); }

W['void RangeFrame(int startX, int startY, int endX, int endY)'](L - 2, TOWER_TOP - 2, R + 2, G + 3);
`Castelo pronto em ${cx}, ${G}: ${R - L + 1} de largura, ${G - TOWER_TOP + 1} de altura`;
