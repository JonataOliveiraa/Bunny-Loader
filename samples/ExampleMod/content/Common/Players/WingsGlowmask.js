const Main = Terraria.Main;
const DrawData = Terraria.DataStructures.DrawData;
const construct = 'void .ctor(Texture2D texture, Vector2 position, Rectangle sourceRect, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, int inactiveLayerDepth)';

export class WingsGlowmask extends PlayerDrawLayer {
    static Layers = new Map();

    static RegisterData(wingSlot, data) {
        if (!Number.isInteger(wingSlot) || wingSlot <= 0) throw new RangeError('wingSlot precisa ser positivo');
        if (!data || !data.Texture || !data.Color) throw new TypeError('informe Texture e Color');
        const frames = data.Frames ?? 4, height = data.Texture.Height, width = data.Texture.Width;
        if (!Number.isInteger(width) || width <= 0 || !Number.isInteger(height) || height <= 0) throw new RangeError('textura precisa ter dimensoes positivas');
        if (!Number.isInteger(frames) || frames <= 0 || height % frames !== 0) throw new RangeError('Frames precisa dividir a altura da textura');
        const offset = data.Offset ?? { X: -9, Y: 2 };
        if (!Number.isFinite(offset.X) || !Number.isFinite(offset.Y)) throw new TypeError('Offset precisa ter X e Y finitos');
        if (!this.Layers.has(wingSlot)) this.Layers.set(wingSlot, {
            ...data, Frames: frames, Width: width, FrameHeight: height / frames, Offset: { X: offset.X, Y: offset.Y }
        });
    }

    static TryGetValue(slot, result) {
        const data = this.Layers.get(slot);
        if (!data) return false;
        result.value = data;
        return true;
    }

    GetDefaultPosition() { return PlayerDrawLayer.AfterParent(PlayerDrawLayers.Wings); }

    GetDefaultVisibility(drawInfo) {
        const player = drawInfo.drawPlayer;
        if (player.dead || player.invis) return false;
        const wings = player.wings;
        return wings > 0 && WingsGlowmask.Layers.has(wings);
    }

    Draw(drawInfo) {
        const player = drawInfo.drawPlayer, data = WingsGlowmask.Layers.get(player.wings);
        if (!data) return;
        const frameIndex = player.wingFrame;
        if (frameIndex < 0 || frameIndex >= data.Frames) return;
        const texture = data.Texture, frameHeight = data.FrameHeight, directions = player.Directions;
        const offset = data.Offset;
        const position = drawInfo.Position, screen = Main.screenPosition;
        const x = Math.floor(position.X - screen.X + player.width / 2 + offset.X * directions.X);
        const y = Math.floor(position.Y - screen.Y + player.height - player.bodyFrame.Height / 2 + 7 + offset.Y * directions.Y);
        const color = player['Color GetImmuneAlphaPure(Color newColor, float alphaReduction)'](data.Color, drawInfo.shadow);
        const drawData = DrawData.new();
        drawData[construct](texture, Vector2.new(x, y), Rectangle.new(0, frameHeight * frameIndex, data.Width, frameHeight),
            color, player.bodyRotation, Vector2.new(data.Width / 2, frameHeight / 2), 1, drawInfo.playerEffect, 0);
        drawData.shader = drawInfo.cWings;
        ModPlayer.AddDrawData(drawInfo, drawData);
    }
}
