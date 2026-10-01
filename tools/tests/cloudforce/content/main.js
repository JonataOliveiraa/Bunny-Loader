// Nuvem de mod na tela sem esperar o sorteio (o peso delas é pequeno): metade
// das nuvens ativas vira o primeiro tipo de mod. No celular o desenho do
// horizonte (NextHorizonRenderer.DrawCloud) lia a máscara da nuvem sem conferir
// o limite e o fundo inteiro sumia; com a máscara dos tipos de mod, o fundo fica.
const Main = Terraria.Main;
const T = Terraria.GameContent.TextureAssets;

function force() {
    const first = Terraria.ID.CloudID.Count;
    if (T.Cloud.length <= first || !T.Cloud[first]) return bl.log('cloudforce: nenhuma nuvem de mod carregada');
    const texture = T.Cloud[first].Value;
    let changed = 0;
    for (let i = 0; i < 200; i++) {
        const cloud = Main.cloud[i];
        if (!cloud.active || i % 2) continue;
        cloud.type = first;
        cloud.width = Math.trunc(texture.Width * cloud.scale);
        cloud.height = Math.trunc(texture.Height * cloud.scale);
        changed++;
    }
    const masks = bl.classOf('', 'TextureMaskManager').CloudMasks;
    bl.log('cloudforce: ' + changed + ' nuvens viraram o tipo ' + first + ' (máscaras: ' + masks.length + ', a do tipo ' + (first < masks.length && masks[first] ? 'existe' : 'FALTA') + ')');
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames % 1800 === 300) {
        try { force(); } catch (e) { bl.log('cloudforce: erro ' + e); }
    }
});
bl.log('cloudforce: carregado');

export default class DiagCloudForce extends Mod {}
