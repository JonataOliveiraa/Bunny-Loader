export class ExampleGolfBall extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/' + this.constructor.name;
    }

    SetDefaults() {
        this.DefaultToGolfBall(ModProjectile.getTypeByName('ExampleGolfBallProjectile'));
    }
}
