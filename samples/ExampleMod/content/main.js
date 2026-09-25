import { ExampleItem } from './Content/Items/ExampleItem.js';
import { ExampleMeleeWeapon } from './Content/Items/Weapons/Melee/ExampleMeleeWeapon.js';
import { ExampleBullet } from './Content/Items/Ammo/ExampleBullet.js';
import { ExampleGun } from './Content/Items/Weapons/Ranged/ExampleGun.js';
import { ExampleBulletProjectile } from './Content/Projectiles/ExampleBulletProjectile.js';
import { ExampleSlimeNPC } from './Content/NPCs/ExampleSlimeNPC.js';
import { ExampleSoul } from './Content/Items/Materials/ExampleSoul.js';
import { ExamplePickaxe } from './Content/Items/Tools/ExamplePickaxe.js';
import { ExampleHamaxe } from './Content/Items/Tools/ExampleHamaxe.js';
import { ExampleGolfBall } from './Content/Items/ExampleGolfBall.js';
import { ExampleGolfBallProjectile } from './Content/Projectiles/ExampleGolfBallProjectile.js';
import { ExampleShotgun } from './Content/Items/Weapons/Ranged/ExampleShotgun.js';
import { ExampleRocketLauncher } from './Content/Items/Weapons/Ranged/ExampleRocketLauncher.js';

ModNPC.register(ExampleSlimeNPC);

ModProjectile.register(ExampleBulletProjectile);
ModProjectile.register(ExampleGolfBallProjectile);

ModItem.register(ExampleItem);
ModItem.register(ExampleMeleeWeapon);
ModItem.register(ExampleBullet);
ModItem.register(ExampleGun);
ModItem.register(ExampleSoul);
ModItem.register(ExamplePickaxe);
ModItem.register(ExampleHamaxe);
ModItem.register(ExampleGolfBall);
ModItem.register(ExampleShotgun);
ModItem.register(ExampleRocketLauncher);

bl.log(`Example Mod: ExampleItem = ${ModItem.getTypeByName('ExampleItem')}, ` +
       `Espada = ${ModItem.getTypeByName('ExampleMeleeWeapon')}, ` +
       `Bala = ${ModItem.getTypeByName('ExampleBullet')}, ` +
       `Arma = ${ModItem.getTypeByName('ExampleGun')}, ` +
       `projetil da bala = ${ModProjectile.getTypeByName('ExampleBulletProjectile')}, ` +
       `slime = ${ModNPC.getTypeByName('ExampleSlimeNPC')}`);
