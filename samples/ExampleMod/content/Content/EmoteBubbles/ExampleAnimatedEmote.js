const { EmoteID } = Terraria.GameContent.UI;

const FRAMES = 8;        // quadros de 34 x 28 na textura, lado a lado
const FRAME_SPEED = 10;  // quadros do jogo por quadro do emote

// Um emote com animação própria (o do ExMod): oito cores em vez dos dois
// quadros do jogo. A animação é de cada bolha (this.EmoteBubble), não da
// classe: duas bolhas ao mesmo tempo não se atrapalham.
export class ExampleAnimatedEmote extends ModEmoteBubble {
    SetStaticDefaults() {
        this.AddToCategory(EmoteID.Category.General);
    }

    // false: o jogo não anima; o frame vai de 0 a 7.
    UpdateFrame() {
        const bubble = this.EmoteBubble;
        if (++bubble.frameCounter >= FRAME_SPEED) {
            bubble.frameCounter = 0;
            bubble.frame = (bubble.frame + 1) % FRAMES;
        }
        return false;
    }

    GetFrame() {
        return Rectangle.new(34 * this.EmoteBubble.frame, 0, 34, 28);
    }

    // No menu, os dois primeiros quadros (a animação do menu é de dois).
    GetFrameInEmoteMenu(frame, frameCounter) {
        return Rectangle.new(34 * frame, 0, 34, 28);
    }
}
