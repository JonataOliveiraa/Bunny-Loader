// Array [,] do jogo (TextureAssets.Players, Asset<Texture2D>[variante, textura]):
//   - a ponte vê o array como uma fila só, linha por linha: o [x, y] do C# é
//     arr[x * colunas + y], e arr.length é o total;
//   - conferido pelo nome de cada asset ("Images/Player_<variante>_<textura>",
//     o PlayerDataInitializer.LoadVariant) e contra o próprio C#:
//     System.Array.GetLength/GetValue/SetValue;
//   - escrever pelo índice corrido aparece no GetValue(x, y), e vice-versa;
//   - a armadilha: arr[x, y] em JS é o operador vírgula, vira arr[y];
//   - no fim troca a cabeça do jogador local por listras (fica até reiniciar).
// Loga "array2d <caso>: ok | FALHOU".
const Main = Terraria.Main;
const TextureAssets = Terraria.GameContent.TextureAssets;
const PlayerVariantID = Terraria.ID.PlayerVariantID;
const PlayerTextureID = Terraria.ID.PlayerTextureID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('array2d ' + label + ': ok');
        else { fails++; bl.log('array2d ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('array2d ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function run() {
    const arr = TextureAssets.Players;
    const rows = PlayerVariantID.Count;
    const cols = PlayerTextureID.Count;
    const at = (x, y) => x * cols + y;
    bl.log('array2d info: ' + String(arr) + ' length=' + arr.length + ' PlayerVariantID.Count=' + rows + ' PlayerTextureID.Count=' + cols);

    check('length = linhas * colunas', () => arr.length === rows * cols || arr.length + ' != ' + rows + '*' + cols);

    check('nome do asset em [x, y] é Player_x_y', () => {
        // Variante 0 carrega tudo menos a 14 (Asset.Empty). A 4 (FemaleStarter)
        // começa como cópia da 0 (CopyVariant(4, 0)) e carrega só 3..13.
        const owner = (x, y) => (x === 4 && (y < 3 || y > 13) ? 0 : x);
        const bad = [];
        let seen = 0;
        for (const x of [0, 4]) {
            for (let y = 0; y < cols; y++) {
                const a = arr[at(x, y)];
                if (!a || y === 14) continue;
                const name = String(a.Name).replace(/\\/g, '/');
                seen++;
                if (!name.endsWith('Player_' + owner(x, y) + '_' + y)) bad.push('[' + x + ',' + y + ']=' + name);
            }
        }
        bl.log('array2d nomes: [0,0]=' + arr[at(0, 0)].Name + ' [4,11]=' + arr[at(4, 11)].Name + ' (' + seen + ' conferidos)');
        return (seen > 20 && bad.length === 0) || bad.slice(0, 5).join(' ') || 'só ' + seen;
    });

    const GetLength = System.Array['int GetLength(int dimension)'];
    const GetValue = System.Array['object GetValue(int index1, int index2)'];
    const SetValue = System.Array['void SetValue(object value, int index1, int index2)'];

    check('System.Array.GetLength(0/1) = linhas/colunas', () => {
        const l0 = GetLength(arr, 0), l1 = GetLength(arr, 1);
        return (l0 === rows && l1 === cols) || l0 + 'x' + l1;
    });

    check('GetValue(x, y) do C# === arr[x * colunas + y] em todas as posições', () => {
        const bad = [];
        for (let x = 0; x < rows; x++) {
            for (let y = 0; y < cols; y++) {
                if (GetValue(arr, x, y) !== arr[at(x, y)]) bad.push(x + ',' + y);
            }
        }
        return bad.length === 0 || 'diferentes em ' + bad.slice(0, 8).join(' ');
    });

    check('escrever arr[x * colunas + y] aparece no GetValue(x, y)', () => {
        const x = 3, y = 5;
        const old = arr[at(x, y)];
        const other = arr[at(0, 0)];
        arr[at(x, y)] = other;
        const seen = GetValue(arr, x, y);
        arr[at(x, y)] = old;
        const back = GetValue(arr, x, y);
        return (seen === other && back === old && other !== old) || 'seen=' + (seen === other) + ' back=' + (back === old);
    });

    check('SetValue(v, x, y) do C# aparece em arr[x * colunas + y]', () => {
        const x = 7, y = 2;
        const old = arr[at(x, y)];
        const other = arr[at(0, 0)];
        SetValue(arr, other, x, y);
        const seen = arr[at(x, y)];
        SetValue(arr, old, x, y);
        return (seen === other && arr[at(x, y)] === old) || 'seen=' + (seen === other);
    });

    check('armadilha: arr[x, y] em JS vira arr[y]', () => {
        // eslint-disable-next-line no-sequences
        const v = arr[(3, 5)];
        return v === arr[5] || 'não é arr[5]';
    });

    check('cabeça do jogador local listrada', () => {
        const skin = Main.LocalPlayer.skinVariant;
        const tex = bl.loadTextureAsset('stripes.png');
        arr[at(skin, 0)] = tex;   // [skinVariant, PlayerTextureID.Head]
        bl.log('array2d cabeça: skinVariant=' + skin + ' -> ' + arr[at(skin, 0)].Name);
        return GetValue(arr, skin, 0) === tex || 'o C# não viu a troca';
    });
}

let done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    done = true;
    run();
    bl.log('array2d FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('array2d: carregado');

export default class TestArray2d extends Mod {}
