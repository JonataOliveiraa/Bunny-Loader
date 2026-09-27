// Example Mod: uma parte do ExampleMod do tModLoader, refeita para o Bunny Loader.
//
// A mesma estrutura do tModLoader:
//
//   main.js          esta classe, a do mod (obrigatória)
//   Assets/          Textures/, Sounds/ e Music/
//   Common/          o que não é coisa nova: ModPlayer, ModSystem, Global*
//   Content/         itens, NPCs, projéteis, blocos, buffs, pets...
//   Localization/    en-US.json, pt-BR.json...
//
// Toda classe exportada em Content/ e Common/ é registrada sozinha, e a
// textura dela sai de Assets/Textures/ no mesmo caminho do arquivo
// (Content/Items/ExampleItem.js -> Assets/Textures/Items/ExampleItem.png).
export default class ExampleMod extends Mod {
    PostSetupContent() {
        bl.log(`Example Mod: ExampleItem = ${ModItem.getTypeByName('ExampleItem')}, ` +
               `Espada = ${ModItem.getTypeByName('ExampleMeleeWeapon')}, ` +
               `Bala = ${ModItem.getTypeByName('ExampleBullet')}, ` +
               `Arma = ${ModItem.getTypeByName('ExampleGun')}, ` +
               `projetil da bala = ${ModProjectile.getTypeByName('ExampleBulletProjectile')}, ` +
               `slime = ${ModNPC.getTypeByName('ExampleSlimeNPC')}`);
    }
}
