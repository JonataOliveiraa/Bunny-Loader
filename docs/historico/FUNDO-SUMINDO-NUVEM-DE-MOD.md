# O fundo que some (só o céu azul) com nuvem de mod

Rodada de 2026-09-30, no Galaxy A15 do usuário, com o Example Mod. Às vezes o
fundo de superfície inteiro sumia e só o céu ficava, até fechar o jogo.
Ferramentas: [`tools/tests/bgwatch`](../../tools/tests/bgwatch) (observa o
fundo), [`tools/tests/cloudforce`](../../tools/tests/cloudforce) (força nuvem de
mod na tela) e o logcat do celular pelo ZeroTier. Trecho do log:
[`dados/2026-09-30/fundo-sumindo-nuvem-de-mod.log`](dados/2026-09-30/fundo-sumindo-nuvem-de-mod.log).

> **Confirmado em 2026-10-10** pelo [`tools/tests/horizonclouds`](../../tools/tests/horizonclouds),
> no MuMu com o `NextHorizonRenderer` ligado: com 13 nuvens de mod na tela, as
> três passadas de nuvens fecham (`CloudsEnd` 3 por quadro) e o fundo desenha as
> 4 camadas, como sem nuvem de mod. Sem a máscara da nuvem de mod (o estado de
> antes da correção), o defeito volta: `CloudsEnd` 0, 1 camada, só o céu na tela.

## O que acontecia

1. O desenho do horizonte do celular (`NextHorizonRenderer.DrawCloud`, a opção
   "HorizonShaders") lê `TextureMaskManager.CloudMasks[cloud.type]` **sem
   conferir o limite** do array. A máscara diz que pedaços de 16 × 16 da nuvem
   tapam o sol (com o `Main.SunMask`).
2. O `CloudLoader` crescia o `TextureAssets.Cloud` para as nuvens de mod (tipos
   depois do `CloudID.Count`), mas não o `CloudMasks`. Com uma nuvem de mod na
   tela, a leitura passava do fim do array e dava `NullReferenceException`
   (lixo não nulo ali derrubaria o jogo).
3. A exceção saía no meio do desenho com o `SpriteBatch_Cloud` aberto. Todo
   quadro seguinte, o `CloudsStart` tentava abrir o lote de novo
   (`Begin cannot be called again until End has been successfully called`) e o
   `DrawSurfaceBG` parava ali: só o céu.

No MuMu nunca tinha aparecido porque a nuvem de mod tem peso pequeno no sorteio.
O desenho do horizonte está ligado lá também (`Main.HorizonShadersEnabled`), ao
contrário do que se achava.

## A correção

`CloudLoader.#GrowMasks`: na instalação das nuvens (depois do
`XNAUnityRunner.Awake`, que carrega as máscaras), o `CloudMasks` cresce até os
tipos de mod e cada nuvem de mod ganha uma `TextureMask` com o tamanho da
textura e `outputWidth` 0: o `IsSolid` devolve false sempre, a nuvem é desenhada
e só não escurece o sol. E uma nuvem cuja textura não carregou não entra mais no
sorteio (o `TextureAssets.Cloud` nulo quebraria o mesmo desenho).

## Para a próxima tabela

O celular tem tabelas por tipo que o PC não tem (máscaras por textura, o
`CloudMasks`, o `BackgroundMasks`) e as lê sem conferir o limite. Conteúdo de
mod que ganha tipo novo tem de crescer todas elas, não só a da textura.
