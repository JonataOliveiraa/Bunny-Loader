// Efeito do mod com o shader ao lado (Rift.frag), registrado pelo autoload.
export class Rift extends ModScreenEffect {
    Warp = 0.02;

    SetDefaults() {
        this.Radius = 80;
        this.Color = [0.6, 0.2, 1.0, 1.0];
    }

    Update() {
        this.Uniforms.uWarp = this.Warp * (1 + 0.5 * Math.sin(this.Age / 10));
    }
}
