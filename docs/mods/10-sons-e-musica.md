# 10. Sons e música

Um mod pode trazer os próprios sons e músicas: o tiro de uma arma, o grito de
um NPC, a música de um chefe. O jeito é o do tModLoader: `SoundStyle` para
sons, `SoundEngine.PlaySound` para tocar na hora e `MusicLoader` com
`ModNPC.Music` para música.

Pré-requisito: os guias de [itens](05-itens.md) e de [NPCs](07-npcs.md).

## Os arquivos

Ponha o áudio dentro de `content/`, em qualquer pasta. O Example Mod usa
`Sounds/` e `Music/`:

```
content/
  Sounds/Items/Guns/ExampleGun.ogg
  Music/Ropocalypse2.ogg
```

Formatos: **OGG** (o recomendado), MP3 e WAV. O caminho vai **sem a
extensão**, relativo a `content/`, como no tModLoader: `'Sounds/Items/Guns/ExampleGun'`.

## Som de item e de NPC

```js
SetDefaults() {
    // ...
    this.Item.UseSound = new SoundStyle('Sounds/Items/Guns/ExampleGun', {
        Volume: 0.9,
        PitchVariance: 0.2,
        MaxInstances: 3,
    });
}
```

O mesmo vale para `this.NPC.HitSound` e `this.NPC.DeathSound`. O jogo toca o
som quando tocaria o dele: no uso do item, no golpe, na morte. O volume cai
com a distância até o centro da tela e segue o volume de efeitos do jogo.

| Opção | |
|---|---|
| `Volume` | 0 a 1. Padrão 1. |
| `Pitch` | Tom, em oitavas: 1 = uma acima, -1 = uma abaixo. Padrão 0. |
| `PitchVariance` | Variação sorteada a cada toque, em oitavas: 0,2 = até 0,1 para cada lado. |
| `MaxInstances` | Quantos deste soam juntos. Padrão 1; 0 = sem limite. |
| `SoundLimitBehavior` | Passou do limite: `SoundLimitBehavior.ReplaceOldest` (padrão) corta o mais velho; `IgnoreNew` não toca o novo. |

As chaves também valem em minúsculas (`volume`, `pitchVariance`...).

Criar o `SoundStyle` dentro do `SetDefaults` não custa nada: o mesmo arquivo
com as mesmas opções devolve **o mesmo** objeto, e o arquivo é lido uma vez só.

Se o arquivo não existe, o `SoundStyle` não lança erro (isso deixaria o item
pela metade): ele fica mudo e o log diz qual arquivo faltou.

## Tocar um som na hora

```js
SoundEngine.PlaySound(style);                  // sem posição: volume cheio
SoundEngine.PlaySound(style, npc.Center);      // cai com a distância
SoundEngine.PlaySound(Terraria.ID.SoundID.Item1, player.Center);   // um do jogo
```

Com um som de mod, `PlaySound` devolve um número (o som tocando), ou 0 se não
tocou: longe demais, volume de efeitos em 0, ou `MaxInstances` com `IgnoreNew`.
Com esse número:

```js
const tocando = SoundEngine.PlaySound(style, player.Center);
SoundEngine.StopSound(tocando);
SoundEngine.FindActiveSound(style);   // o mais novo deste que ainda soa, ou 0
```

## Música

A música de um NPC toca enquanto ele está perto da tela, como a de um chefe:

```js
SetDefaults() {
    // ...
    this.Music = MusicLoader.GetMusicSlot('Music/Ropocalypse2');
}
```

- `MusicLoader.GetMusicSlot(caminho)` devolve o número da música (depois dos
  do jogo), ou 0 se o arquivo não existe. `GetMusicSlot(bl.mod, caminho)`, do
  jeito do tModLoader, também vale.
- `this.Music` também aceita uma música do jogo: `Terraria.ID.MusicID.Boss2`.
  -1 (o padrão) deixa o jogo escolher.
- Pode mudar no meio da luta, na `AI`: a fase 2 com outra música. Dê um
  valor já no `SetDefaults`: é ali que o Bunny Loader vê que o NPC tem
  música (um NPC que nasce com -1 e só ganha música na `AI` fica sem ela, a
  não ser que outro NPC de mod já tenha ligado a música de mod).
- Com dois NPCs de música por perto, ganha o de `SceneEffectPriority` maior
  (`SceneEffectPriority.BossLow` é o padrão; vão de `None` a `BossHigh`).
- "Perto" é o mesmo do tModLoader: a até 5000 px da tela.

A troca é igual à do jogo entre duas músicas dele: a nova entra baixinho e
vai subindo, e a que tocava só começa a sair quando a nova já se ouve — uns 4
segundos, sem corte. Vale na chegada do chefe e no fim da luta (ou quando você
sai do mundo), quando a do jogo volta do mesmo jeito. A do mod toca em laço,
no volume de música do jogo.

`MusicLoader.MusicExists(caminho)` diz se o arquivo existe, e
`MusicLoader.IsMusicPlaying(slot)` se a música está tocando agora.

## Como funciona, e o limite

O Terraria Mobile é Unity por baixo, e **esta build não sabe criar som
novo**: o `AudioClip.Create` e tudo que ele usa foram cortados do jogo na
compilação. Então o áudio de mod toca pelo **Android** (SoundPool para sons,
MediaPlayer para música), e o Bunny Loader faz ele se comportar como o do
jogo: volume de efeitos e de música, distância e lado da tela, pausa quando o
jogo vai para o fundo.

Na prática:

- **Som curto.** Efeito é para ser curto, de até alguns segundos. Áudio longo é
  música.
- A música é lida aos poucos (streaming), então o tamanho do arquivo não pesa
  na memória.
- No multijogador, cada aparelho toca o próprio áudio, como o jogo faz.

## Diferenças para o tModLoader

| tModLoader | Bunny Loader |
|---|---|
| `new SoundStyle("ExampleMod/Assets/Sounds/X") { Volume = 0.9f }` | `new SoundStyle('Sounds/X', { Volume: 0.9 })`, relativo a `content/`, sem o nome do mod |
| `SoundStyle` é um struct | o `new` devolve um `LegacySoundStyle` do jogo (o tipo do `UseSound`): `instanceof SoundStyle` dá `false` |
| `SoundEngine.PlaySound` devolve `SlotId` | devolve o número do som (0 = não tocou) |
| `Variants`, `IsLooped`, callback de atualização | ainda não |
| `MusicLoader.GetMusicSlot(Mod, caminho)` | também `GetMusicSlot(caminho)`; não há carga automática de `Music/` |
| `ModBiome.Music`, `ModSceneEffect`, caixa de música | ainda não: só `ModNPC.Music` |

## Referência rápida

| | |
|---|---|
| `new SoundStyle(caminho, { Volume, Pitch, PitchVariance, MaxInstances, SoundLimitBehavior })` | um som do mod; vai no `UseSound`, `HitSound`, `DeathSound` |
| `SoundEngine.PlaySound(estilo, posição?)` | tocar agora; som de mod devolve o número (0 = não tocou) |
| `SoundEngine.FindActiveSound(estilo)`, `SoundEngine.StopSound(n)` | o que ainda soa; parar |
| `MusicLoader.GetMusicSlot(caminho)` | o número de uma música do mod (0 = não existe) |
| `this.Music`, `this.SceneEffectPriority` (no `ModNPC`) | a música enquanto ele está perto; quem ganha |
| `MusicLoader.IsMusicPlaying(slot)`, `MusicLoader.MusicExists(caminho)` | está tocando? o arquivo existe? |

Os testes `tools/tests/sounds` e `tools/tests/music` cobrem cada caso deste
guia; o segundo usa o chefe do Example Mod.
