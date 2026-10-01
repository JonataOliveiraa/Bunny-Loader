// Um emote de mod, como o ModEmoteBubble do tModLoader: a textura da classe
// (Content/EmoteBubbles/X.js -> Content/EmoteBubbles/X.png), dois
// quadros de 34 x 28 lado a lado. Uma instância por tipo; durante cada método
// this.EmoteBubble é a bolha em questão (o EmoteBubble do jogo).
// AddToCategory(EmoteID.Category.X) o põe no menu de emotes.
class ModEmoteBubble {
    Type = undefined;
    Texture = this.constructor.name;
    EmoteBubble = null;

    SetStaticDefaults() {}
    AddToCategory(categoryId) { EmoteBubbleLoader.AddToCategory(this, categoryId); }

    // No menu de emotes só se liberado.
    IsUnlocked() { return true; }
    OnSpawn() {}
    // false: a bolha não anima sozinha (o frame fica por conta do mod).
    UpdateFrame() { return true; }
    // O quadro na textura; null: o do jogo (frame 0 ou 1 de 34 x 28).
    GetFrame() { return null; }
    // O quadro no menu de emotes (frame: 0 ou 1, a animação do menu); null:
    // metade da textura, como na bolha.
    GetFrameInEmoteMenu(frame, frameCounter) { return null; }
    // false: a bolha não é desenhada (PostDraw roda do mesmo jeito).
    PreDraw(spriteBatch, texture, position, frame, origin, spriteEffects) { return true; }
    PostDraw(spriteBatch, texture, position, frame, origin, spriteEffects) {}

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModEmoteBubble)) {
            throw new TypeError('ModEmoteBubble.register(Classe): passe a classe, que estende ModEmoteBubble');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);
        EmoteBubbleLoader.Add(inst);
        return inst.Type;
    }
}
