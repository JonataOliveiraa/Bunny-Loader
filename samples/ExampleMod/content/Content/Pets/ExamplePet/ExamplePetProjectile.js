const { ProjectileID } = Terraria.ID;

export class ExamplePetProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Pets/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.projFrames[this.Type] = 4;
        Terraria.Main.projPet[this.Type] = true;
        ProjectileID.Sets.CharacterPreviewAnimations[this.Type] = ProjectileID.Sets.SimpleLoop(0, 4, 6, false)
            ['SettingsForCharacterPreview WithOffset(float x, float y)'](-10, -20)
            .WithSpriteDirection(-1);
    }

    SetDefaults() {
        this.CloneDefaults(ProjectileID.ZephyrFish);
        this.AIType = ProjectileID.ZephyrFish;
    }

    PreAI(proj) {
        Terraria.Main.player[proj.owner].zephyrfish = false;
        return true;
    }

    AI(proj) {
        const player = Terraria.Main.player[proj.owner];
        if (!player.dead && player.FindBuffIndex(ModBuff.getTypeByName('ExamplePetBuff')) >= 0) proj.timeLeft = 2;
    }
}
