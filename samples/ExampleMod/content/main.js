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
import { ExampleSpearProjectile } from './Content/Projectiles/ExampleSpearProjectile.js';
import { ExampleFlailProjectile } from './Content/Projectiles/ExampleFlailProjectile.js';
import { ExampleYoyoProjectile } from './Content/Projectiles/ExampleYoyoProjectile.js';
import { ExampleWhipProjectile } from './Content/Projectiles/ExampleWhipProjectile.js';
import { ExampleSwingingEnergySwordProjectile } from './Content/Projectiles/ExampleSwingingEnergySwordProjectile.js';
import { ExampleDrillProjectile } from './Content/Projectiles/ExampleDrillProjectile.js';
import { ExampleAdvancedAnimatedProjectile } from './Content/Projectiles/ExampleAdvancedAnimatedProjectile.js';
import { ExampleSpear } from './Content/Items/Weapons/Melee/ExampleSpear.js';
import { ExampleFlail } from './Content/Items/Weapons/Melee/ExampleFlail.js';
import { ExampleYoyo } from './Content/Items/Weapons/Melee/ExampleYoyo.js';
import { ExampleWhip } from './Content/Items/Weapons/Melee/ExampleWhip.js';
import { ExampleSwingingEnergySword } from './Content/Items/Weapons/Melee/ExampleSwingingEnergySword.js';
import { ExampleDrill } from './Content/Items/Tools/ExampleDrill.js';
import { ExampleMagicWeapon } from './Content/Items/Weapons/Magic/ExampleMagicWeapon.js';
import { ExampleLaserHoldout } from './Content/Projectiles/ExampleLaserHoldout.js';
import { ExampleLaserBeam } from './Content/Projectiles/ExampleLaserBeam.js';
import { ExampleHookProjectile } from './Content/Projectiles/ExampleHookProjectile.js';
import { ExampleBobber } from './Content/Projectiles/ExampleBobber.js';
import { ExampleLaserWeapon } from './Content/Items/Weapons/Magic/ExampleLaserWeapon.js';
import { ExampleHookItem } from './Content/Items/Tools/ExampleHookItem.js';
import { ExampleFishingRod } from './Content/Items/Tools/ExampleFishingRod.js';
import { ExampleRecipes } from './Content/Global/ExampleRecipes.js';
import { ExampleDefenseBuff } from './Content/Buffs/ExampleDefenseBuff.js';
import { ExampleBuffPotion } from './Content/Items/Consumables/ExampleBuffPotion.js';
import { ExamplePlayer } from './Content/Players/ExamplePlayer.js';
import { ExampleDashPlayer } from './Content/Players/ExampleDashPlayer.js';
import { ExampleDefenseDebuff } from './Content/Buffs/ExampleDefenseDebuff.js';
import { ExampleShield } from './Content/Items/Accessories/ExampleShield.js';
import { ExampleStatAccessory } from './Content/Items/Accessories/ExampleStatAccessory.js';
import { ExampleBoots } from './Content/Items/Accessories/ExampleBoots.js';
import { WaspNest } from './Content/Items/Accessories/WaspNest.js';

ModBuff.register(ExampleDefenseBuff);
ModBuff.register(ExampleDefenseDebuff);

ModPlayer.register(ExamplePlayer);
ModPlayer.register(ExampleDashPlayer);

ModNPC.register(ExampleSlimeNPC);

ModProjectile.register(ExampleBulletProjectile);
ModProjectile.register(ExampleGolfBallProjectile);
ModProjectile.register(ExampleSpearProjectile);
ModProjectile.register(ExampleFlailProjectile);
ModProjectile.register(ExampleYoyoProjectile);
ModProjectile.register(ExampleWhipProjectile);
ModProjectile.register(ExampleSwingingEnergySwordProjectile);
ModProjectile.register(ExampleDrillProjectile);
ModProjectile.register(ExampleAdvancedAnimatedProjectile);
ModProjectile.register(ExampleLaserHoldout);
ModProjectile.register(ExampleLaserBeam);
ModProjectile.register(ExampleHookProjectile);
ModProjectile.register(ExampleBobber);

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
ModItem.register(ExampleSpear);
ModItem.register(ExampleFlail);
ModItem.register(ExampleYoyo);
ModItem.register(ExampleWhip);
ModItem.register(ExampleSwingingEnergySword);
ModItem.register(ExampleDrill);
ModItem.register(ExampleMagicWeapon);
ModItem.register(ExampleLaserWeapon);
ModItem.register(ExampleHookItem);
ModItem.register(ExampleFishingRod);
ModItem.register(ExampleBuffPotion);
ModItem.register(ExampleShield);
ModItem.register(ExampleStatAccessory);
ModItem.register(ExampleBoots);
ModItem.register(WaspNest);

ModSystem.register(ExampleRecipes);

bl.log(`Example Mod: ExampleItem = ${ModItem.getTypeByName('ExampleItem')}, ` +
       `Espada = ${ModItem.getTypeByName('ExampleMeleeWeapon')}, ` +
       `Bala = ${ModItem.getTypeByName('ExampleBullet')}, ` +
       `Arma = ${ModItem.getTypeByName('ExampleGun')}, ` +
       `projetil da bala = ${ModProjectile.getTypeByName('ExampleBulletProjectile')}, ` +
       `slime = ${ModNPC.getTypeByName('ExampleSlimeNPC')}`);
