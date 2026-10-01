// As raridades de mod, como o RarityLoader do tModLoader: o tipo sai no
// registro, depois das 12 do jogo (ItemRarityID.Count). O jogo trata toda
// raridade >= 11 como roxa, e cada lugar ganha um hook, só com raridade de mod
// registrada:
// - ItemRarity.Initialize: a tabela das cores (a etiqueta [i:] do chat);
// - Item.GetPopupRarityColor: o texto que sobe ao pegar o item;
// - Item.Prefix: o prefixo sobe ou desce a raridade e o jogo a prende em 11;
//   aqui decide o GetPrefixedRarity (sem isso o item perdia a raridade ao cair
//   no chão com prefixo);
// - Main.MouseTextInner: o nome do item no chão sob o cursor. O nome no
//   tooltip é do TooltipLoader.
class RarityLoader {
    static ByType = new Map();
    static VanillaCount = 12;   // ItemRarityID.Count
    static #DRAW_STRING = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';

    static get RarityCount() { return RarityLoader.VanillaCount + RarityLoader.ByType.size; }

    static Add(inst) {
        inst.Type = RarityLoader.RarityCount;
        RarityLoader.ByType.set(inst.Type, inst);
        if (RarityLoader.ByType.size === 1) Ready.Add(() => RarityLoader.#Install(), 'setup');
        Hooks.Once('item.Tooltips', TooltipLoader.Install);   // a cor do nome no tooltip
        return inst.Type;
    }

    static GetRarity(type) { return RarityLoader.ByType.get(type) || null; }

    // A cor de uma raridade de mod; branco se o RarityColor falhar.
    static ColorOf(type) {
        const inst = RarityLoader.ByType.get(type);
        const color = inst && Safe.Run(inst.constructor.name + '.RarityColor', () => inst.RarityColor);
        return color || Color.White;
    }

    static #Install() {
        for (const inst of RarityLoader.ByType.values()) {
            Safe.Run(inst.constructor.name + '.SetStaticDefaults', () => inst.SetStaticDefaults());
        }

        const min = { minType: RarityLoader.VanillaCount };
        const ItemRarity = Terraria.GameContent.UI.ItemRarity;
        ItemRarity['void Initialize()'].hook((original) => {
            original();
            const table = ItemRarity._rarities;
            for (const type of RarityLoader.ByType.keys()) table.Add(type, RarityLoader.ColorOf(type));
        });
        Safe.Run('raridades de mod: tabela de cores', () => ItemRarity['void Initialize()']());

        Terraria.Item['Color GetPopupRarityColor(int itemRarity)'].hook(
            (original, rarity) => RarityLoader.ByType.has(rarity) ? RarityLoader.ColorOf(rarity) : original(),
            { ...min, arg: 0 });

        Terraria.Item['bool Prefix(int prefixWeWant, out bool rolledPrefixIsTopTier)'].hook(
            (original, self, want, topTier) => {
                const base = self.rare;
                const ok = original(self, want, topTier);
                if (ok && want !== -3 && self.prefix !== 0) RarityLoader.#Prefixed(self, base);
                return ok;
            }, { ...min, field: 'rare' });

        Terraria.Main['void MouseTextInner(Main.MouseTextCache info, bool worldMouse)'].hook(
            (original, self, info, worldMouse) => {
                if (!RarityLoader.ByType.has(info.rare) || Terraria.Main.HoverItem.type >= 1 ||
                    (info.buffTooltip !== null && info.buffTooltip !== '')) return original();
                RarityLoader.#DrawMouseText(info, worldMouse);
            });
    }

    // O que o Item.Prefix do tModLoader faz com a raridade de mod: o jogo já
    // aplicou o prefixo e prendeu a raridade em 11; o valor do prefixo (o
    // valueMult) dá o degrau de -2 a +2, e a raridade decide.
    static #Prefixed(item, base) {
        const inst = RarityLoader.ByType.get(base);
        if (!inst) return;

        const refs = Array.from({ length: 10 }, () => new Ref(0));
        item['bool TryGetPrefixStatMultipliersForItem(int rolledPrefix, out float dmg, out float kb, out float spd, out float size, out float shtspd, out float mcst, out int crt, out int tagdmg, out int arpen, out float value)'](
            item.prefix, ...refs);
        const value = Number(refs[9].value);
        const offset = value >= 1.2 ? 2 : value >= 1.05 ? 1 : value <= 0.8 ? -2 : value <= 0.95 ? -1 : 0;

        let rare = Safe.Run(inst.constructor.name + '.GetPrefixedRarity', () => inst.GetPrefixedRarity(offset, value));
        if (!Number.isInteger(rare)) rare = base;
        if (rare > -11) rare = Math.min(Math.max(rare, -1), RarityLoader.RarityCount - 1);
        item.rare = rare;
    }

    // O Main.MouseTextInner do celular para um texto só (o item no chão), com
    // a cor da raridade de mod. Porte do GST378.
    static #DrawMouseText(info, worldMouse) {
        const Main = Terraria.Main;
        const text = info.cursorText;
        if (text === null) return;

        let X = worldMouse ? Main.worldMouseX : Main.mouseX;
        let Y = worldMouse ? Main.worldMouseY : Main.mouseY;
        let offset = info.X === -1 && info.Y === -1 ? 14 : 10;
        if (Main.ThickMouse) offset += 6;
        X += offset;
        if (!worldMouse) Y += offset;

        const font = Terraria.GameContent.FontAssets.MouseText.Value;
        const size = font['Vector2 MeasureString(string text)'](text);
        const w = info.hackedScreenWidth !== -1 && info.hackedScreenHeight !== -1 ? info.hackedScreenWidth : Main.screenWidth;
        const h = info.hackedScreenWidth !== -1 && info.hackedScreenHeight !== -1 ? info.hackedScreenHeight : Main.screenHeight;
        if (X + size.X + 4 > w) X = (w - size.X - 4) | 0;
        if (Y + size.Y + 4 > h) Y = (h - size.Y - 4) | 0;

        const alpha = worldMouse ? 1 : XNAUIInputLayer.UITextAlphaCustom(Main.tooltipTime, 1, 0);
        if (alpha <= 0) return;

        let color = RarityLoader.ColorOf(info.rare);
        if (info.diff === 1) color = Main.mcColor;
        else if (info.diff === 2) color = Main.hcColor;
        color = Color.Multiply(Color.new(color.R, color.G, color.B, 255), alpha);

        const shadow = Color.Multiply(Color.Black, alpha);
        const batch = Main.spriteBatch;
        const draw = (x, y, c) => batch[RarityLoader.#DRAW_STRING](font, text, Vector2.new(x, y), c, 0, Vector2.Zero, 1, 0, 0);
        draw(X, Y - 2, shadow);
        draw(X, Y + 2, shadow);
        draw(X - 2, Y, shadow);
        draw(X + 2, Y, shadow);
        draw(X, Y, color);
    }
}
