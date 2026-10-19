// O celular escolhe alguns ícones por tabelas fixas de tipos do jogo. A página
// do baú e da cômoda (GUIPageIcons.GetCategoryTexture) só conhece os tiles 21,
// 29, 88, 97 e 467, e a da loja procura a cabeça do morador num dicionário de
// imagens prontas: o de mod virava o baú genérico e a bolsa. A placa
// (GUINPCDialogue.DrawSign) troca o tile por um item fixo (55 é a Placa, 85 a
// Lápide...): a de mod virava o item 0, quadro vazio e sem nome.
class PageIconLoader {
    static WantPageIcons() {
        Hooks.Once('ui.pageIcons', () => Safe.Run('ícones das páginas', PageIconLoader.#HookPageIcons));
    }

    static WantSignItem() {
        Hooks.Once('ui.signItem', () => Safe.Run('item da placa', PageIconLoader.#HookSignItem));
    }

    static #HookPageIcons() {
        const Main = Terraria.Main;
        const PageIcons = bl.classOf('', 'GUIPageIcons');
        const Category = PageIcons.Category;
        const CHEST = Category.Chest, DRESSER = Category.Dresser, SHOP = Category.Shop;

        // O HUD pede um ícone por quadro mesmo com o inventário fechado: só as
        // três categorias entram no JS.
        for (const category of [CHEST, DRESSER, SHOP]) bl.hookMarks.set('ui.pageIcon', category);

        PageIcons['Texture2D GetCategoryTexture(GUIPageIcons.Category category, bool selected)'].hook((original, self, category, selected) => {
            let texture = null;
            if (category === CHEST) texture = PageIconLoader.#ChestIcon(Main.LocalPlayer);
            else if (category === DRESSER) texture = PageIconLoader.#TileIcon(Main.interactedDresserTopLeftX, Main.interactedDresserTopLeftY);
            else if (category === SHOP) texture = PageIconLoader.#ShopIcon();

            return texture || original(self, category, selected);
        }, { minType: 0, arg: 0, marks: 'ui.pageIcon' });
    }

    static #ChestIcon(player) {
        return player.chest >= 0 ? PageIconLoader.#TileIcon(player.chestX, player.chestY) : null;
    }

    static #TileIcon(x, y) {
        const item = PageIconLoader.#ItemOf(x, y);
        return item > 0 ? PageIconLoader.#ItemTexture(item) : null;
    }

    // A cabeça que o loader pôs em TextureAssets.NpcHead (a do shimmer, se houver).
    static #ShopIcon() {
        const talk = TownNPCLoader.TalkingTo();
        if (!talk) return null;

        const type = talk.npc.type;
        const shimmer = talk.npc.IsShimmerVariant ? bl.npcs.headSlot(type, true) : -1;
        const head = shimmer >= 0 ? shimmer : bl.npcs.headSlot(type);
        return head >= 0 ? Terraria.GameContent.TextureAssets.NpcHead[head].Value : null;
    }

    // O item do tile de mod em (x, y), pelo estilo: o que ele solta ao quebrar.
    static #ItemOf(x, y) {
        const m = TileLoader.At(x, y);
        if (!m) return 0;

        const style = TileLoader.HasObjectData(m.Type) ? TileLoader.StyleAt(x, y) : 0;
        return TileLoader.GetItemDropFromTypeAndStyle(m.Type, style);
    }

    static #ItemTexture(item) {
        const asset = Terraria.GameContent.TextureAssets.Item[item];
        return asset ? asset.Value : null;
    }

    // O original desenha o quadro vazio; por cima vão o nome e o item, com as
    // mesmas medidas do DrawSign (margem de 22, 8 px abaixo da âncora).
    static #HookSignItem() {
        const Main = Terraria.Main;
        const Layout = bl.classOf('', 'NPCDialogue_Layout');
        const GUIString = bl.classOf('', 'GUIString');
        const anchored = bl.classOf('', 'LayoutCalculator').GetAnchoredPosition;
        const drawName = GUIString['void Draw(String_Layout layout, string value)'];
        const DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';

        bl.classOf('', 'GUINPCDialogue')['void DrawSign()'].hook((original, self) => {
            original(self);

            const player = Main.LocalPlayer;
            const sign = player.sign >= 0 ? Main.sign[player.sign] : null;
            const item = sign ? PageIconLoader.#ItemOf(sign.x, sign.y) : 0;
            const texture = item > 0 ? PageIconLoader.#ItemTexture(item) : null;
            if (!texture) return;

            const layout = Layout.Instance;
            const box = layout.DialogueItem;
            const animation = Main.itemAnimations[item];
            const frame = animation ? animation.GetFrame(texture, -1) : Rectangle.new(0, 0, texture.Width, texture.Height);
            const position = anchored(box.AnchorControl, box.Anchor, box.Location);
            const size = box.Size;
            const scale = Math.min((size.X - 22) / frame.Width, (size.Y - 22) / frame.Height);

            drawName(layout.DialogueItemName, Terraria.Lang.GetItemNameValue(item));
            Main.spriteBatch[DRAW](texture, Vector2.new(position.X, position.Y + 8), frame, Color.White, 0,
                Vector2.new(Math.trunc(frame.Width / 2), Math.trunc(frame.Height / 2)), scale, 0, 0);
        });
    }
}
