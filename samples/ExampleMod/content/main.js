import { ExampleItem } from './Content/Items/ExampleItem.js';
import { ExampleMeleeWeapon } from './Content/Items/Weapons/Melee/ExampleMeleeWeapon.js';
import { ExampleBullet } from './Content/Items/Ammo/ExampleBullet.js';
import { ExampleGun } from './Content/Items/Weapons/Ranged/ExampleGun.js';
import { ExampleBulletProjectile } from './Content/Projectiles/ExampleBulletProjectile.js';
import { ExampleSlimeNPC } from './Content/NPCs/ExampleSlimeNPC.js';

ModItem.register(ExampleItem);
ModItem.register(ExampleMeleeWeapon);
ModItem.register(ExampleBullet);
ModItem.register(ExampleGun);

bl.log(`Example Mod: ExampleItem = ${ModItem.getTypeByName('ExampleItem')}, ` +
       `Espada = ${ModItem.getTypeByName('ExampleMeleeWeapon')}, ` +
       `Bala = ${ModItem.getTypeByName('ExampleBullet')}, ` +
       `Arma = ${ModItem.getTypeByName('ExampleGun')}, ` +
       `projetil da bala = ${ExampleBulletProjectile}, slime = ${ExampleSlimeNPC}`);
