// Uma bolha de 30x30 (a moldura é a textura inteira) que sobe e encolhe.
export class ExampleBubble extends ModDust {
    OnSpawn(dust) {
        dust.noGravity = true;
        dust.frame = Rectangle.new(0, 0, 30, 30);
    }

    Update(dust) {
        dust.position.X += dust.velocity.X;
        dust.position.Y += dust.velocity.Y;
        dust.scale -= 0.01;

        if (dust.scale < 0.75) dust.active = false;
        return false;
    }
}
