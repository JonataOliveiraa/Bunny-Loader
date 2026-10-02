// Cada projétil de mod ganha a própria cópia do molde em proj.ModProjectile.
class ModProjectile {
    Type = undefined;
    DisplayName = '';
    Texture = this.constructor.name;
    // Usa a IA deste projétil do jogo (o tipo é trocado só durante a IA dele).
    AIType = 0;

    get Projectile() { return Entities.Of(this); }

    Clone(newProjectile) { return Entities.Clone(this); }

    // Como o do tModLoader: roda antes do SetStaticDefaults, com this.Projectile
    // já montado pelo SetDefaults. Sem o projHook, o botão de gancho do
    // celular (QuickGrapple) não acha o item.
    AutoStaticDefaults() {
        const proj = this.Projectile;
        if (!proj) return;

        if (proj.hostile) Terraria.Main.projHostile[this.Type] = true;
        if (proj.aiStyle === ProjAIStyleID.Hook) Terraria.Main.projHook[this.Type] = true;
    }

    SetStaticDefaults() {}
    SetDefaults(proj) {}
    PostStaticDefaults() {}
    PostSetDefaults(proj) {}
    PostSetupContent() {}

    OnSpawn(proj) {}
    PreAI(proj) { return true; }
    AI(proj) {}
    PostAI(proj) {}
    SendExtraAI(writer) {}
    ReceiveExtraAI(reader) {}
    PreKill(proj, timeLeft) { return true; }
    OnKill(proj, timeLeft) {}
    // width, height, fallThrough e hitboxCenterFrac são Ref (.value): a caixa
    // que colide com os blocos e se ela atravessa plataformas. false: o
    // projétil atravessa os blocos neste quadro.
    TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac) { return true; }
    // false mantém o projétil vivo depois de bater num bloco. Lacaio
    // (Main.projPet) recebe em todo quadro com choque, sem morrer.
    OnTileCollide(proj, oldVelocity) { return true; }
    OnHitNPC(proj, npc) {}
    OnHitPlayer(proj, player) {}
    Colliding(proj, projHitbox, targetHitbox) { return undefined; }
    CanDamage(proj) { return true; }
    MinionContactDamage(proj) { return false; }
    ModifyDamageHitbox(proj, hitbox) {}
    CanCutTiles(proj) { return undefined; }
    CutTiles(proj) {}
    GetAlpha(proj, lightColor) { return undefined; }
    // false: sem a corrente/linha/fio que o jogo desenha (o gancho desenha a sua aqui).
    PreDrawExtras(proj) { return true; }
    PreDraw(proj, lightColor) { return true; }
    PostDraw(proj, lightColor) {}
    // No molde, antes de o gancho existir.
    CanUseGrapple(player, type) { return true; }
    UseGrapple(player, type) { return type; }
    GrappleCanLatchOnTo(proj, player, tile) { return undefined; }

    CloneDefaults(type) {
        const source = Terraria.Projectile.new();
        source['void .ctor()']();
        source['void SetDefaults(int Type)'](type);

        for (const key of ProjectileLoader.CLONED_FIELDS) {
            try {
                this.Projectile[key] = source[key];
            } catch (e) {
                // campo que esta versão não tem
            }
        }
    }

    DefaultToSpear() { this.Projectile['void DefaultToSpear()'](); }
    DefaultToYoyo() { this.Projectile['void DefaultToYoyo()'](); }
    DefaultToFlail() { this.Projectile['void DefaultToFlail()'](); }
    DefaultToWhip() { this.Projectile['void DefaultToWhip()'](); }
    DefaultToDrillOrChainsaw() { this.Projectile['void DefaultToDrillOrChainsaw()'](); }
    DefaultToKite() { this.Projectile['void DefaultToKite()'](); }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModProjectile)) {
            throw new TypeError('ModProjectile.register(Classe): passe a classe, que estende ModProjectile');
        }

        GoreLoader.Autoload();
        Entities.Define(Terraria.Projectile, 'ModProjectile');

        const inst = new cls();
        const name = cls.name;
        Templates.Adopt(cls, inst);
        inst.Texture = ModFiles.ContentTexture(inst, cls);

        const type = bl.projectiles.register({
            name,
            texture: ModFiles.Texture(inst.Texture),
            displayName: inst.DisplayName || Lang.Localized('ProjectileName', name) || name,
            setDefaults(proj) {
                const m = Entities.Bind(inst.Clone(proj), proj, 'ModProjectile');
                m.SetDefaults(proj);
                m.PostSetDefaults(proj);
            },
            setStaticDefaults(t) {
                // O molde do tModLoader: um projétil com o SetDefaults do mod,
                // visto como this.Projectile até o fim do SetStaticDefaults.
                const template = Terraria.Projectile.new();
                template['void .ctor()']();
                template['void SetDefaults(int Type)'](t);

                inst.__entity = bl.addressOf(template);
                try {
                    inst.AutoStaticDefaults();
                    inst.SetStaticDefaults();
                    inst.PostStaticDefaults();
                } finally {
                    inst.__entity = 0;
                }

                // O Damage do celular só chama o Colliding (os pontos do
                // chicote, o Colliding do mod) para os tipos desta tabela; os
                // outros ferem pela interseção com o retângulo do projétil.
                const Sets = Terraria.ID.ProjectileID.Sets;
                if (Sets.IsAWhip[t] || Hooks.Overrides(cls, ModProjectile, 'Colliding')) Sets.IsAComplexCollision[t] = true;
                bl.projectiles.setFrames(t, Terraria.Main.projFrames[t]);
            },
        });
        inst.Type = type;
        Lang.Follow('ProjectileName.' + name, inst.DisplayName || Lang.Localized('ProjectileName', name));
        ProjectileLoader.ByType.set(type, inst);

        Ready.Add(() => inst.PostSetupContent());
        ProjectileLoader.Hook(cls, type);
        return type;
    }

    static isModType(type) { return bl.projectiles.isModProjectile(type); }
    static isModProjectile(proj) { return !!proj && bl.projectiles.isModProjectile(proj.type); }
    static getTypeByName(name) { return bl.projectiles.typeOf(name); }
    static getModProjectile(type) { return ProjectileLoader.ByType.get(type); }
    static getByName(name) { return ProjectileLoader.ByType.get(bl.projectiles.typeOf(name)); }
}
