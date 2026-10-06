// O tooltip do item, como o do tModLoader: ModifyTooltips com os nomes das
// linhas do jogo, e os hooks de desenho PreDrawTooltip/PostDrawTooltip e
// PreDrawTooltipLine/PostDrawTooltipLine (com DrawableTooltipLine).
//
// O Main.MouseText_DrawItemTooltip do celular vira JS (porte do GST378) só
// para o item que pede: mod com um desses hooks (ModItem, GlobalItem, prefixo
// com GetTooltipLines), raridade de mod ou preço em moeda própria; o resto é
// o do jogo. Se o porte
// quebrar, o log diz uma vez e o tooltip volta a ser o do jogo.
//
// O celular desenha cada linha com DrawString, sem o parser de tags
// [c/RRGGBB:texto] do PC: a linha com tag é desenhada aqui, trecho a trecho, e
// a medida dela ignora as tags.
class TooltipLoader {
    static #COLOR_TAG = /\[c\/([0-9a-fA-F]{6}):([^\]]*)\]/g;
    static #ITEM_HOOKS = ['ModifyTooltips', 'PreDrawTooltip', 'PostDrawTooltip', 'PreDrawTooltipLine', 'PostDrawTooltipLine'];
    static #DRAW_STRING = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
    static #DRAW_RIGHT = 'void DrawStringMultiLineRight(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)';
    static #MEASURE = 'Vector2 MeasureString(string text)';
    static #broken = false;

    static Hex(color) {
        if (typeof color === 'string') return color.replace(/^#/, '').slice(0, 6).toUpperCase();

        const hex = (v) => Math.min(Math.max(Math.round(v || 0), 0), 255).toString(16).padStart(2, '0');
        return (hex(color.R) + hex(color.G) + hex(color.B)).toUpperCase();
    }

    static HasTags(text) { return typeof text === 'string' && text.indexOf('[c/') >= 0; }
    static StripTags(text) { return text.replace(TooltipLoader.#COLOR_TAG, '$2'); }

    static Segments(text) {
        const tag = TooltipLoader.#COLOR_TAG;
        const out = [];
        let at = 0;

        tag.lastIndex = 0;
        for (let m; (m = tag.exec(text));) {
            if (m.index > at) out.push({ text: text.slice(at, m.index) });

            const v = parseInt(m[1], 16);
            out.push({ text: m[2], rgb: [(v >> 16) & 255, (v >> 8) & 255, v & 255] });
            at = m.index + m[0].length;
        }
        if (at < text.length) out.push({ text: text.slice(at) });

        return out;
    }

    static Install() {
        const Main = Terraria.Main;
        const drawTooltip = Main['void MouseText_DrawItemTooltip(Main.MouseTextCache info, int rare, byte diff, int X, int Y)'];

        drawTooltip.hook((original, self, info, rare, diff) => {
            const item = Main.HoverItem;
            if (TooltipLoader.#broken || !TooltipLoader.#Wants(item)) return original();

            const touch = ControlsTouchTagHandler.ControlsTouchSnippet;
            try {
                TooltipLoader.#Draw(item, diff);
            } catch (e) {
                TooltipLoader.#broken = true;
                bl.error("tooltip: the mod drawing failed, falling back to the game's: " + e + (e && e.stack ? '\n' + e.stack : ''));
            } finally {
                touch.OpenButtonFromEquipment = false;
            }
            return undefined;
        });

        TooltipLoader.#HookDrawing(drawTooltip);
        Safe.Run('tooltip: popup do guia', TooltipLoader.#HookGuidePopup);
    }

    // O popup do guia de criação (GUICraftGuidePopup.UpdateText) monta o texto
    // do item com o GetLinesInfo: lá dentro, as linhas vêm do ModifyTooltips
    // (e do prefixo de mod), sem cor (o popup desenha o texto cru). Ideia do GST378.
    static #HookGuidePopup() {
        // O whileIn pede o método de fora hookado: um hook de passagem.
        const updateText = GUICraftGuidePopup['void UpdateText()'];
        updateText.hook((original) => original());
        Terraria.Main['void MouseText_DrawItemTooltip_GetLinesInfo(Item item, ref int yoyoLogo, ref int researchLine, ref int materialsLine, float oldKB, ref int numLines, string[] toolTipLine, bool[] preFixLine, bool[] badPreFixLine, ref int setBonusLine, ref Color setBonusColour, ref int sharedLine)'].hook(
            (original, item, yoyo, research, materials, oldKB, numLines, lines, pre, bad, setBonus, setColor, shared) => {
                if (TooltipLoader.#broken || !TooltipLoader.#Wants(item)) return original();

                let list;
                try {
                    const built = VanillaTooltips.Build(item, oldKB);
                    list = TooltipLoader.#Modify(item, built.lines, built.prefixEnd);
                    setColor.value = built.setBonusColor;
                } catch (e) {
                    TooltipLoader.#broken = true;
                    bl.error("tooltip: the mod lines in the crafting guide failed, falling back to the game's: " + e);
                    return original();
                }

                const count = Math.min(list.length, lines.length);
                const at = (test) => {
                    const i = list.findIndex(test);
                    return i < count ? i : -1;
                };
                for (let i = 0; i < count; i++) {
                    const line = list[i];
                    lines[i] = TooltipLoader.StripTags(String(line.Text));
                    pre[i] = !!(line.IsModifier || line.IsModifierBad);
                    bad[i] = !!line.IsModifierBad;
                }
                numLines.value = count;
                yoyo.value = at((l) => l.OneDropLogo);
                research.value = at((l) => l.__role === 'research');
                materials.value = at((l) => l.__role === 'materials');
                setBonus.value = at((l) => l.__role === 'setBonus');
                shared.value = at((l) => l.__role === 'shared');
                return undefined;
            }, { whileIn: updateText });
    }

    // As linhas do GetTooltipLines do prefixo de mod também na janela da reforja.
    static HookReforge() {
        GUIReforgePopup['void UpdatePrefixes(Item reforgeItem)'].hook((original, self, item) => {
            original();
            const extra = item && item.prefix >= PrefixLoader.VanillaCount ? PrefixLoader.TooltipLines(item) : null;
            if (!extra) return;

            const text = self.toolTipLine, bad = self.badPreFixLine;
            for (const line of extra) {
                if (self.numLines >= text.length) break;
                text[self.numLines] = TooltipLoader.StripTags(String(line.Text));
                bad[self.numLines] = !!line.IsModifierBad;
                self.numLines++;
            }
        });
    }

    static #Wants(item) {
        if (!item || item.type < 1) return false;
        if (RarityLoader.ByType.has(item.rare)) return true;
        // Moeda própria na loja: o jogo do celular pinta o preço com uma cor
        // fixa (246, 100, 120) e ignora o CurrencyTextColor; aqui ele vale.
        if (item.shopSpecialCurrency !== -1 && Terraria.Main.npcShop > 0) return true;
        // Classe de dano que o jogo não entende: o texto dele diria a das flags.
        if (item.damage > 0 && DamageClassLoader.IsHooked(DamageClassLoader.ItemClass(item))) return true;

        const m = ItemLoader.Of(item);
        if (m && TooltipLoader.#ITEM_HOOKS.some((h) => Hooks.Overrides(m.constructor, ModItem, h))) return true;

        const p = item.prefix >= PrefixLoader.VanillaCount ? PrefixLoader.GetPrefix(item.prefix) : null;
        if (p && Hooks.Overrides(p.constructor, ModPrefix, 'GetTooltipLines')) return true;

        return globalItems.AnyWith(item, TooltipLoader.#ITEM_HOOKS);
    }

    // O ModifyTooltips do tModLoader: as linhas do prefixo de mod depois das
    // do jogo, o do ModItem, os dos GlobalItem, e fora as escondidas.
    static #Modify(item, lines, prefixEnd) {
        const prefixLines = prefixEnd >= 0 ? PrefixLoader.TooltipLines(item) : null;
        if (prefixLines) lines.splice(prefixEnd, 0, ...prefixLines);

        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'ModifyTooltips')) {
            Safe.Run(m.constructor.name + '.ModifyTooltips', () => m.ModifyTooltips(item, lines));
        }
        if (!item.IsAir) globalItems.Each(item, 'ModifyTooltips', (g) => g.ModifyTooltips(item, lines));

        return lines.filter((l) => l instanceof TooltipLine && l.Visible !== false);
    }

    static #PreDrawTooltip(item, lines, x, y) {
        let ok = true;
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'PreDrawTooltip')) {
            ok = Safe.Run(m.constructor.name + '.PreDrawTooltip', () => m.PreDrawTooltip(item, lines, x, y)) !== false;
        }
        globalItems.Each(item, 'PreDrawTooltip', (g) => {
            if (g.PreDrawTooltip(item, lines, x, y) === false) ok = false;
        });
        return ok;
    }

    static #PostDrawTooltip(item, lines) {
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'PostDrawTooltip')) {
            Safe.Run(m.constructor.name + '.PostDrawTooltip', () => m.PostDrawTooltip(item, lines));
        }
        globalItems.Each(item, 'PostDrawTooltip', (g) => g.PostDrawTooltip(item, lines));
    }

    static #PreDrawTooltipLine(item, line, yOffset) {
        let ok = true;
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'PreDrawTooltipLine')) {
            ok = Safe.Run(m.constructor.name + '.PreDrawTooltipLine', () => m.PreDrawTooltipLine(item, line, yOffset)) !== false;
        }
        globalItems.Each(item, 'PreDrawTooltipLine', (g) => {
            if (g.PreDrawTooltipLine(item, line, yOffset) === false) ok = false;
        });
        return ok;
    }

    static #PostDrawTooltipLine(item, line) {
        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'PostDrawTooltipLine')) {
            Safe.Run(m.constructor.name + '.PostDrawTooltipLine', () => m.PostDrawTooltipLine(item, line));
        }
        globalItems.Each(item, 'PostDrawTooltipLine', (g) => g.PostDrawTooltipLine(item, line));
    }

    // A cor do nome, pela raridade (a de mod pelo RarityColor).
    static #NameColor(rare, diff, expert) {
        const Main = Terraria.Main;
        if (diff === 1) return Color.new(Main.mcColor.R, Main.mcColor.G, Main.mcColor.B);
        if (diff === 2) return Color.new(Main.hcColor.R, Main.hcColor.G, Main.hcColor.B);
        if (expert || rare === -12) return Color.new(Main.DiscoR, Main.DiscoG, Main.DiscoB);

        switch (rare) {
            case -13: return Color.new(255, (Main.masterColor * 200) | 0, 0);
            case -11: return Color.new(255, 175, 0);
            case -1: return Color.new(130, 130, 130);
            case 1: return Color.new(150, 150, 255);
            case 2: return Color.new(150, 255, 150);
            case 3: return Color.new(255, 200, 150);
            case 4: return Color.new(255, 150, 150);
            case 5: return Color.new(255, 150, 255);
            case 6: return Color.new(210, 160, 255);
            case 7: return Color.new(150, 255, 10);
            case 8: return Color.new(255, 255, 10);
            case 9: return Color.new(5, 200, 255);
            case 10: return Color.new(255, 40, 100);
            case 11: return Color.new(180, 40, 255);
        }
        if (RarityLoader.ByType.has(rare)) {
            const c = RarityLoader.ColorOf(rare);
            return Color.new(c.R, c.G, c.B);
        }
        return rare > 11 ? Color.new(180, 40, 255) : Color.White;
    }

    // A cor de cada linha sai do papel dela (não do índice): linha que o mod
    // põe ou tira no ModifyTooltips não troca a cor das outras.
    static #LineColor(line, ctx) {
        const alpha = ctx.alpha;
        const mul = (c) => Color.Multiply(Color.new(c.R, c.G, c.B), alpha);
        if (line.OverrideColor) return mul(line.OverrideColor);

        switch (line.__role) {
            case 'name': return mul(ctx.nameColor);
            case 'price': return ctx.priceColor;
            case 'setBonus': return mul(ctx.setBonusColor);
            case 'research': return mul(Terraria.ID.Colors.JourneyMode);
            case 'shared': return mul(Terraria.Main.OurFavoriteColor);
        }
        if (line.IsModifier || line.IsModifierBad) return mul(line.IsModifierBad ? Color.new(190, 120, 120) : Color.new(120, 190, 120));
        return mul(Color.White);
    }

    // As linhas de preço (loja) e de custo da reforja.
    static #PriceLines(item, lines, ctx) {
        const { Lang, Main, Utils } = Terraria;
        const alpha = ctx.alpha;
        const coinColor = (plat, gold, silver) => Color.Multiply(
            plat > 0 ? Color.new(220, 220, 198) : gold > 0 ? Color.new(224, 201, 92) : silver > 0 ? Color.new(181, 192, 193) : Color.new(246, 138, 96), alpha);
        const coinText = (plat, gold, silver, copper) =>
            (plat > 0 ? plat + ' ' + Lang.inter[15].Value + ' ' : '') + (gold > 0 ? gold + ' ' + Lang.inter[16].Value + ' ' : '') +
            (silver > 0 ? silver + ' ' + Lang.inter[17].Value + ' ' : '') + (copper > 0 ? copper + ' ' + Lang.inter[18].Value + ' ' : '');
        const add = (name, text) => {
            const line = new TooltipLine('Terraria', name, text);
            line.__role = 'price';
            lines.push(line);
        };

        if (Main.npcShop > 0 && item.value >= 0) {
            const selling = new Ref(0), buying = new Ref(0);
            Main.LocalPlayer.GetItemExpectedPrice(item, selling, buying);
            const isCoin = item.type >= 71 && item.type <= 74;
            const price = isCoin ? 0 : Number(item.isAShopItem || item.buyOnce ? buying.value : selling.value);

            if (item.shopSpecialCurrency !== -1) {
                const CustomCurrencyManager = Terraria.GameContent.UI.CustomCurrencyManager;
                const buffer = new Array(4).fill('').makeGeneric('string');
                const count = new Ref(0);
                CustomCurrencyManager.GetPriceText(item.shopSpecialCurrency, buffer, count, price);
                if (count.value > 0) {
                    const text = String(buffer[count.value - 1]);
                    add('SpecialPrice', text.slice(text.indexOf(':') + 1).replaceAll(']', ''));
                    ctx.priceColor = Color.Multiply(TooltipLoader.#CurrencyColor(item.shopSpecialCurrency), alpha);
                }
            } else if (price > 0) {
                let total;
                if (item.buy) {
                    total = price * item.stack;
                } else {
                    const unit = Math.max(Math.floor(price / 5), 1);
                    total = unit * item.stack;
                    const sellback = Main.shopSellbackHelper.GetAmount(item);
                    if (sellback > 0) total += (Number(buying.value) - unit) * Math.min(sellback, item.stack);
                }
                total = Math.max(total, 1);
                const plat = Math.floor(total / 1000000); total -= plat * 1000000;
                const gold = Math.floor(total / 10000); total -= gold * 10000;
                const silver = Math.floor(total / 100); total -= silver * 100;
                const copper = total;

                add('Price', (item.buy ? Lang.tip[50].Value : Lang.tip[49].Value) + ' ' + coinText(plat, gold, silver, copper));
                ctx.priceColor = coinColor(plat, gold, silver);
            } else if (!isCoin && item.type !== 3817) {
                add('Price', Lang.tip[51].Value);
                ctx.priceColor = Color.Multiply(Color.new(120, 120, 120), alpha);
            }
        }

        if (Main.InReforgeMenu && item.reforge) {
            const cost = GUIInstance.Active.GUIReforge.ItemReforgeCost(item);
            if (cost > 0) {
                const coins = Utils.CoinsSplit(cost);
                add('ReforgePrice', VanillaTooltips.TextValue('Mobile.ReforgePrice') + ' ' + coinText(coins[3], coins[2], coins[1], coins[0]));
                ctx.priceColor = coinColor(coins[3], coins[2], coins[1]);
            }
        }
    }

    // A cor do preço numa moeda própria: o CurrencyTextColor dela. Moeda sem
    // ele (outra classe que não a CustomCurrencySingleCoin) ou com a cor
    // vazia (alfa 0, nunca escrita) fica com a cor do jogo.
    static #CurrencyColor(id) {
        const currency = Terraria.GameContent.UI.CustomCurrencyManager._currencies.get_Item(id);
        const c = currency && 'CurrencyTextColor' in currency ? currency.CurrencyTextColor : null;
        return c && c.A > 0 ? Color.new(c.R, c.G, c.B) : Color.new(246, 100, 120);
    }

    // O Main.MouseText_DrawItemTooltip do celular, com os hooks de mod. Porte do GST378.
    static #Draw(item, diff) {
        const Main = Terraria.Main;
        const { FontAssets, TextureAssets } = Terraria.GameContent;
        const batch = Main.spriteBatch;

        const tips = Settings.Instance.Tooltips;
        const opaqueBox = Main.SettingsEnabled_OpaqueBoxBehindTooltips;
        const distX = tips.Distance.X, distY = tips.Distance.Y;
        const pad = opaqueBox ? tips.Backing.Size : Vector2.Zero;
        const padX = pad.X, padY = pad.Y;
        const scale = tips.Scale;
        const drawOffset = tips.DrawOffset;

        // Onde: do lado do dedo que tiver espaço.
        const screenW = Main.screenWidth, screenH = Main.screenHeight;
        const screenHalf = screenW > 889 ? (screenW / 2) | 0 : 445;
        let toRight, x, y;
        if (Main.overloadedMousePositionX !== -1) {
            x = Main.overloadedMousePositionX;
            y = Main.overloadedMousePositionY + (drawOffset.Y | 0);
            Main.overloadedMousePositionX = -1;
            Main.overloadedMousePositionY = -1;
            toRight = true;
        } else {
            toRight = Main.mouseX < screenHalf;
            x = Main.mouseX + (toRight ? 1 : -1) * (drawOffset.X | 0);
            y = Main.mouseY + (drawOffset.Y | 0);
        }
        if (distX > x) x = distX | 0;
        if (distY > y) y = distY | 0;

        const alphaByte = (XNAUIInputLayer.UITextAlphaCustom(Main.inventoryTooltipTime, 1, 0) * 255) | 0;
        if (alphaByte === 0) return;
        const alpha = alphaByte / 255;

        // O empurrão da luva e do cogumelo entra no texto (o jogo faz no HoverItem, uma cópia).
        const oldKB = item.knockBack;
        const me = Main.LocalPlayer;
        let kbMult = item.melee && me.kbGlove ? 2 : 1;
        if (me.kbBuff) kbMult += 0.5;
        if (kbMult !== 1) item.knockBack *= kbMult;
        if (item.ranged && me.shroomiteStealth) item.knockBack *= 1 + (1 - me.stealth) * 0.5;

        const built = VanillaTooltips.Build(item, oldKB);
        const ctx = {
            alpha,
            nameColor: TooltipLoader.#NameColor(item.rare, diff, item.expert),
            setBonusColor: built.setBonusColor,
            priceColor: Color.Multiply(Color.White, alpha),
        };
        TooltipLoader.#PriceLines(item, built.lines, ctx);

        const tooltips = TooltipLoader.#Modify(item, built.lines, built.prefixEnd);
        const count = tooltips.length;
        // BaseScale já com a escala do tooltip do celular (o PC não tem): o mod
        // que desenha a linha com ela sai do tamanho das outras.
        const drawables = tooltips.map((line, i) => {
            const dl = new DrawableTooltipLine(line, i, 0, 0, TooltipLoader.#LineColor(line, ctx));
            dl.BaseScale = Vector2.new(scale, scale);
            return dl;
        });

        // Medida
        const font = FontAssets.MouseText.Value;
        const materials = tooltips.findIndex((l) => l.__role === 'materials');
        const lineW = new Array(count), lineH = new Array(count);
        let matSlotY = 0, sizeX = 0, sizeY = 0;
        for (let i = 0; i < count; i++) {
            let w, h;
            if (i === materials) {
                const layout = Crafting_Layout.Instance;
                const n = GUIInstance.Active.GUICrafting.CraftHoverNumMaterials;
                const perRow = layout.MaterialsGrid.ItemLineCount;
                const cols = Math.min(n, perRow);
                const rows = Math.floor((n + perRow - 1) / perRow);
                const slot = Vector2.Multiply(Terraria.Utils['Vector2 Size(Texture2D tex)'](layout.MaterialBacking.Texture), layout.MaterialItemScale);
                const gap = layout.MaterialsGrid.ExtraElementSpacing;
                matSlotY = slot.Y;
                w = slot.X * cols + gap.X * (cols - 1);
                h = slot.Y * rows + gap.Y * (rows - 1);
                if (item.tooltipContext === 22 || item.tooltipContext === 47) h += layout.MaterialTooltipBackingExtra;
            } else {
                const m = font[TooltipLoader.#MEASURE](tooltips[i].Text);
                w = m.X * scale;
                h = m.Y * scale;
            }
            lineW[i] = w;
            lineH[i] = h;
            if (w > sizeX) sizeX = w;
            sizeY += h;
        }

        if (toRight) {
            if (x + sizeX + distX + padX * 2 > screenW) x = (screenW - sizeX - distX - padX * 2) | 0;
        } else if (x - sizeX - distX - padX * 2 < 0) {
            x = ((padX * 2) | 0) + (distX | 0) + (sizeX | 0);
        }
        if (y + sizeY + distY > screenH) y = (screenH - sizeY - distY) | 0;

        // Com controle: não cobrir a faixa dos botões.
        if (Controller.ControllerActionManager.AnyControllerConnected && GUIInstance.Active.GUIControlsBanner.ShowBanner) {
            const category = GUIPageIconGrouping.GetCategoryFromActiveController();
            if (category !== GUIPageIcons.Category.None) {
                const grouping = InterfaceStyles_Layout.Active.GetCategoryGrouping(category, true);
                if (grouping !== null) {
                    grouping.LoadPageAnchor();
                    const overlap = padY + distY + sizeY + y - ControlAnchor.PageContentRegion.Bottom;
                    if (overlap > 0) y = overlap > y ? 0 : y - (overlap | 0);
                }
            }
        }

        if (opaqueBox) TooltipLoader.#DrawBacking(item, toRight ? x : x - sizeX, y, sizeX + padX * 2, sizeY + padY * 2, tips, alpha);

        const touch = ControlsTouchTagHandler.ControlsTouchSnippet;
        const context = item.tooltipContext;
        touch.OpenButtonFromEquipment = (context >= 8 && context < 13) || (context >= 16 && context < 21);

        const X = new Ref(x), Y = new Ref(y);
        const canDraw = TooltipLoader.#PreDrawTooltip(item, tooltips, X, Y);
        x = Number(X.value) | 0;
        y = Number(Y.value) | 0;

        // As linhas. yOffset é o do tModLoader: o espaço a mais depois de cada linha, de ali em diante.
        const shadow = Color.Multiply(Color.Black, alpha);
        const yOffset = new Ref(0);
        let lineTop = 0;
        for (let i = 0; i < count; i++) {
            const dl = drawables[i];
            const top = y + lineTop;

            if (dl.OneDropLogo) {
                const logo = TextureAssets.OneDropLogo.Value;
                dl.OriginalX = dl.X = (toRight ? x : x - scale * logo.Width) + padX;
                dl.OriginalY = dl.Y = top + padY;
                if (TooltipLoader.#PreDrawTooltipLine(item, dl, yOffset) && canDraw) {
                    batch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](
                        logo, Vector2.new(dl.X, dl.Y), null, Color.Multiply(Color.White, alpha), dl.Rotation, dl.Origin, dl.BaseScale.X, 0, 0);
                }
            } else if (i === materials) {
                const layout = Crafting_Layout.Instance;
                dl.OriginalX = dl.X = (toRight ? x : x - lineW[i]) + padX;
                dl.OriginalY = dl.Y = top + padY;
                if (TooltipLoader.#PreDrawTooltipLine(item, dl, yOffset) && canDraw) {
                    const SpriteBatchItem = Microsoft.Xna.Framework.Graphics.SpriteBatchItem;
                    const off = toRight ? layout.MaterialTooltipOriginOffsetLeft : layout.MaterialTooltipOriginOffsetRight;
                    SpriteBatchItem.ColourScale = alpha;
                    GUIInstance.Active.GUICrafting.DrawMaterialsGridTooltip(Vector2.Add(Vector2.new(dl.X, dl.Y), off));
                    SpriteBatchItem.ColourScale = 1;
                }
            } else {
                dl.OriginalX = dl.X = (toRight ? x : x - (lineW[i] | 0)) + padX;
                dl.OriginalY = dl.Y = top + padY;
                if (TooltipLoader.#PreDrawTooltipLine(item, dl, yOffset) && canDraw) {
                    const right = !toRight && tooltips[i].__role === 'setBonus';
                    TooltipLoader.#DrawText(batch, dl, shadow, right);
                }
            }

            TooltipLoader.#PostDrawTooltipLine(item, dl);
            lineTop += (i === materials ? matSlotY | 0 : lineH[i] | 0) + (Number(yOffset.value) | 0);
        }

        TooltipLoader.#PostDrawTooltip(item, drawables);
    }

    // Quatro sombras e o texto; o bônus de conjunto à esquerda do dedo vai alinhado à direita.
    static #DrawText(batch, dl, shadow, right) {
        const s = dl.Spread;
        const offsets = [[-s, 0], [s, 0], [0, -s], [0, s], [0, 0]];
        const scaleV = dl.BaseScale;
        for (let pass = 0; pass < 5; pass++) {
            const pos = Vector2.new(dl.X + offsets[pass][0], dl.Y + offsets[pass][1]);
            const color = pass < 4 ? shadow : dl.Color;
            if (right) batch[TooltipLoader.#DRAW_RIGHT](dl.Font, dl.Text, pos, color, dl.Rotation, dl.Origin, scaleV, 0, 0);
            else batch[TooltipLoader.#DRAW_STRING](dl.Font, dl.Text, pos, color, dl.Rotation, dl.Origin, dl.BaseScale.X, 0, 0);
        }
    }

    // O fundo do tooltip, na cor de onde o item está (loja, baú, equipamento...).
    static #DrawBacking(item, x, y, w, h, tips, alpha) {
        const settings = Settings.Instance;
        const backing = tips.Backing;
        let color = backing.BackingColor;
        switch (item.tooltipContext) {
            case 29: color = settings.DuplicationTooltipBacking; break;
            case 32: color = settings.VoidItemTooltipBacking; break;
            case 35: color = settings.BannerTooltipBacking; break;
            case 15: color = settings.ShopTooltipBacking; break;
            case 3: color = settings.ChestTooltipBacking; break;
            case 44: color = settings.ResearchTooltipBacking; break;
            case 4: color = settings.BankTooltipBacking; break;
            case 5: color = settings.PrefixTooltipBacking; break;
            case 47: color = settings.CraftingStationTooltipBacking; break;
            case 8: case 9: case 10: case 11: case 12: {
                const loadout = Terraria.Main.LocalPlayer.CurrentLoadoutIndex;
                if (loadout === 0) color = settings.Equip0TooltipBacking;
                else if (loadout === 1) color = settings.Equip1TooltipBacking;
                else if (loadout === 2) color = settings.Equip2TooltipBacking;
                break;
            }
            case 16: case 17: case 18: case 19: case 20: case 33:
                color = settings.Equip0TooltipBacking;
                break;
        }

        const pos = Vector2.new(x, y), size = Vector2.new(w, h);
        GUIPanel.DrawBacking(pos, size, backing.Backing.Texture, Color.Multiply(color, alpha), null);
        GUIPanel.DrawBacking(pos, size, backing.Border.Texture, Color.Multiply(backing.BorderColor, alpha), null);
    }

    // Dentro do tooltip: medida sem as tags, e cada trecho na cor dele.
    static #HookDrawing(drawTooltip) {
        const Graphics = Microsoft.Xna.Framework.Graphics;
        const measure = TooltipLoader.#MEASURE;
        const { HasTags, StripTags } = TooltipLoader;

        Graphics.SpriteFont[measure].hook((original, font, text) =>
            HasTags(text) ? original(font, StripTags(text)) : original(), { whileIn: drawTooltip });

        Graphics.SpriteBatch[TooltipLoader.#DRAW_STRING].hook(
            (original, batch, font, text, pos, color, rotation, origin, scale, effects, depth) => {
                if (!HasTags(text)) return original();

                // A sombra fica preta; o texto pega a cor de cada trecho, com o
                // alfa da linha (as cores do jogo são pré-multiplicadas).
                const shadow = color.R === 0 && color.G === 0 && color.B === 0;
                const alpha = color.A / 255;
                let x = pos.X;
                for (const seg of TooltipLoader.Segments(text)) {
                    if (!seg.text) continue;

                    const c = !shadow && seg.rgb
                        ? Color.new(seg.rgb[0] * alpha, seg.rgb[1] * alpha, seg.rgb[2] * alpha, color.A)
                        : color;
                    original(batch, font, seg.text, Vector2.new(x, pos.Y), c, rotation, origin, scale, effects, depth);
                    x += font[measure](seg.text).X * scale;
                }
                return undefined;
            }, { whileIn: drawTooltip });
    }
}
