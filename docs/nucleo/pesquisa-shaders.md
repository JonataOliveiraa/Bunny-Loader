# Pesquisa de shaders no Bunny Loader

Documento interno de engenharia para orientar a investigação e registrar novas
evidências. Não descreve uma API de shaders já disponível para mods.

**Atualizado:** 2026-10-03. **Estado:** análise estática; nenhum shader próprio
foi carregado ou validado em execução nesta pesquisa.

## Objetivo e conclusão atual

Investigar suporte a efeitos em sprites, tinturas e filtros de tela, com uma
experiência de mod semelhante à do tModLoader. Separar três capacidades:

1. Reutilizar shaders que já acompanham o jogo.
2. Carregar shaders próprios e integrá-los ao desenho do Terraria.
3. Portar efeitos do tModLoader, incluindo seus parâmetros e assets.

A implementação mobile contém uma ponte concreta entre efeitos do Terraria e
materiais da Unity. Reutilizar essa ponte é o caminho mais fundamentado. O
carregamento de um shader novo ainda é o principal ponto sem comprovação.
Não há evidência de compatibilidade direta com arquivos `.fx` ou `.fxc` do
tModLoader, nem de um carregador público de shaders próprio do Bunny.

## Como interpretar as evidências

| Classificação | Significado |
|---|---|
| Confirmado no código do Bunny | Comportamento observado nos arquivos do loader. |
| Confirmado no dump | Classe, campo ou assinatura presente; não comprova o corpo do método. |
| Confirmado no binário | Fluxo ou instrução verificado na biblioteca ARM64, sem executar o jogo. |
| Referência externa | Comportamento documentado pela Unity ou pelo tModLoader; não garante disponibilidade nesta build. |
| Hipótese | Caminho possível que ainda exige investigação ou experimento. |
| Validado em execução | Reservado para uma prova prática com logs e resultado visual. Nenhuma nesta etapa. |

Um nome presente nos metadados, uma string no binário ou uma chamada que não
lança erro não bastam para classificar um shader como funcional.

## Build usada como referência

O [README de refs](../../refs/README.md) identifica o dump atual como Terraria
**1.4.5.8.6**. O cabeçalho de
`terraria1456_assets/src/main/assets/bin/Data/data.unity3d` identifica a Unity
como **2021.3.56f2**. A documentação antiga de hosting trata de 1.4.5.6.4;
não usar seus endereços como referência para esta pesquisa.

Estado do repositório consultado: HEAD `d7d47e4`, com alterações locais
pendentes. O commit identifica o contexto, não uma árvore de trabalho limpa.

Os pares abaixo tiveram SHA-256 idêntico entre a cópia de pesquisa e a cópia
integrada na build:

| Arquivo | Cópia de pesquisa | Cópia integrada |
|---|---|---|
| `libil2cpp.so` | `refs/libil2cpp.so` | `app/src/main/jniLibs/arm64-v8a/libil2cpp.so` |
| `libunity.so` | `refs/libunity.so` | `app/src/main/jniLibs/arm64-v8a/libunity.so` |
| `global-metadata.dat` | `refs/global-metadata.dat` | `terraria1456_assets/src/main/assets/bin/Data/Managed/Metadata/global-metadata.dat` |

SHA-256 registrados:

- `libil2cpp.so`: `f33b48c09e96d17234af40c7d4ce16da52d8fb72a4ead729bfad8dc88a081814`
- `libunity.so`: `8a13b23554371fe0c9c6f55a7f013c1772c99b75cf1bfa8e3c5a6379801f59cf`
- `global-metadata.dat`: `4c904c49d168913d28ef547c4f461e539d926d2ed27c0253ed53a2954e9585f7`

Os binários, assets, `refs/dump.cs` e `refs/script.json` são referências locais
ignoradas pelo Git. Gerar o dump a partir dos próprios arquivos conforme o
README de refs. Revalidar hashes e endereços após qualquer atualização do jogo.

## Descobertas sobre o desenho do jogo

### A camada XNA usa objetos da Unity

O [carregador de texturas](../../app/src/main/cpp/script/api/Texture.cpp)
documenta e utiliza um `UnityEngine.Texture2D` dentro da textura do jogo. O
nome `Microsoft.Xna.Framework.Graphics` não implica uma implementação idêntica
à do Terraria no PC.

No dump existem `Effect`, `EffectPass`, `SpriteBatch`, `RenderTarget2D`,
`GameShaders`, `ArmorShaderData`, `MiscShaderData`, `ScreenShaderData`, `Filter`
e `FilterManager`. O construtor de `EffectPass` recebe explicitamente um
`UnityEngine.Material`.

### Cada shader é procurado em Resources

**Confirmado no binário:** `PixelShader.LoadPass` e `ScreenShader.LoadPass`
concatenam o nome solicitado com um caminho, carregam um `Shader` com
`Resources.Load<Shader>`, criam um material e constroem um `EffectPass`.

| Família | Caminho procurado | Substituto quando falta |
|---|---|---|
| Pixel/sprite | `EffectShaders/PixelShader/<nome>` | `EffectShaders/PixelShader/Default` |
| Tela | `EffectShaders/ScreenShader/<nome>` | `EffectShaders/ScreenShader/Default` |

Nesse caso o jogo também registra avisos, encontrados nas strings locais:
`Using Default as temporary replacement for new pixel shader` e
`Using Default as temporary replacement for new screen shader`.

**Consequência:** registrar um pass com nome novo pode continuar desenhando
sem executar um shader novo. A prova prática precisa verificar o shader
efetivamente associado ao material e produzir uma mudança visual inequívoca.

O fluxo atual fornece um ponto de integração para um material personalizado,
mas não comprova que um asset externo possa ser carregado. Um arquivo solto na
pasta do mod não passa automaticamente a integrar o catálogo de `Resources`.

### Há passes nativos candidatos à primeira prova

`ScreenShader.get_Effect` chama `LoadPass` para nomes como
`FilterColor`, `FilterHeatDistortion`, `FilterWaterDistortion`, `FilterInvert`,
`FilterNoir`, `FilterSepia`, `FilterCRT` e `FilterGraveyard`.

O registro desses nomes foi confirmado no binário. A existência de cada asset
e o resultado de cada efeito nesta build continuam pendentes; o substituto
padrão impede concluir isso apenas pela lista de passes.

### Os parâmetros são encaminhados por classes específicas

**Confirmado no binário:** `Effect.ApplyValuesToMaterial` contém apenas uma
instrução de retorno. `PixelShader` e `ScreenShader` sobrescrevem esse método;
a implementação de tela chama `Material.SetColor`, `SetFloat` e `SetVector`.

O inicializador de `ScreenShader` registra IDs de propriedades como `_Color`,
`_Opacity`, `_SecondaryColor`, `_ShaderTime`, `_ScreenResolution`,
`_ScreenPosition`, `_TargetPosition`, `_Intensity`, `_Progress`, `_Direction`,
`_ImageOffset`, `_ImageSize1`, `_ImageSize2`, `_ImageSize3`, `_VPPI` e `_Zoom`.

Não assumir que adicionar uma entrada a `Effect.Parameters` encaminha um
parâmetro arbitrário ao shader. A integração precisa respeitar as propriedades
esperadas pela classe usada ou fornecer seu próprio encaminhamento.

O jogo também possui `MaterialBuffer`, partições e associação de texturas.
Ainda falta verificar o ciclo de vida completo desses materiais e se alterações
no material base alcançam todas as instâncias usadas no desenho. Não presumir
que mudar um material isolado modifica todos os batches.

### O Bunny já possui um ponto para efeitos de cenário

O [SceneEffectLoader](../../app/src/main/cpp/script/js/mod/Loaders/SceneEffectLoader.js)
chama `SpecialVisuals(player, active)` tanto para efeitos ativos quanto
inativos. Isso permite investigar ativação e desativação por bioma/evento.

No dump, `FilterManager` contém `BeginCapture` e `EndCapture`, recebendo
`RenderTarget2D`. Essa assinatura indica uma estrutura existente para filtros
de tela; não comprova sua disponibilidade em todas as configurações gráficas,
nem quais camadas da imagem, como a interface, entram na captura.

## Carregamento de shaders próprios

### AssetBundles: evidência parcial

No `libunity.so` foram encontradas referências a `AssetBundleManager`,
`AssetBundleCreateRequest`, `AssetBundleRequest`, carregamento/descarregamento,
CRC e incompatibilidade de bundles. `ScriptingAssemblies.json` também lista
`UnityEngine.AssetBundleModule.dll` e
`UnityEngine.UnityWebRequestAssetBundleModule.dll`.

Entretanto, não foi encontrada a classe `AssetBundle` no dump, nem as strings
exatas das chamadas `UnityEngine.AssetBundle::LoadFromFile_Internal`,
`LoadFromMemory_Internal` e `LoadAsset_Internal` nos dois binários consultados.

Isso é compatível com stripping de bindings, mas não o comprova. Também não
prova ausência completa das funções nativas: código pode permanecer sem os
nomes pesquisados. As referências remanescentes não garantem um carregador
acessível e funcional.

**Questão aberta:** localizar um caminho de carregamento utilizável pelo
Bunny, verificar sua assinatura real e seu vínculo com os objetos IL2CPP.
Não presumir que `il2cpp_resolve_icall` resolverá uma chamada apenas porque
ela aparece na documentação da Unity.

O código oficial da Unity 2021.3 documenta os bindings de AssetBundle, mas
serve como referência de investigação, não como prova de que esses bindings
foram preservados no jogo. Ver as fontes externas ao final.

### Formato e compilação

No tModLoader, o `FxcReader` lê um efeito compilado e chama o construtor de
`Effect` com `GraphicsDevice` e os bytes. Esse construtor não aparece na classe
`Effect` do dump mobile analisado.

Uma rota com a Unity exigiria assets de shader compilados para Android, com
versão, variantes e APIs gráficas compatíveis. Usar inicialmente a mesma
Unity 2021.3.56f2 reduz uma variável da investigação, sem garantir compatibilidade.
Não criar o bundle com uma versão mais nova supondo compatibilidade automática.

`Shader.Find` procura um shader existente; não compila o texto de um `.fx`.
O dump marca como obsoleto e sem suporte o construtor de `Material` a partir
de texto de shader. Carregar PNG por `bl.loadTexture` também não oferece um
caminho equivalente para carregar shaders.

O corpo matemático de um efeito pode ser portado, mas ainda será necessário
adaptar entradas de vértices, texturas, propriedades, transparência, coordenadas
de sprites e estrutura do shader da Unity. Não tratar `.fx`, `.fxc` e um asset
de shader da Unity como formatos intercambiáveis.

## Abordagens e escolha provisória

| Abordagem | Benefícios | Limitações e manutenção |
|---|---|---|
| Expor shaders existentes | Aproveita o desenho e os recursos já carregados; menor superfície de integração. | Limita os mods aos programas existentes; ainda exige conferir disponibilidade, isolamento de parâmetros e desativação. |
| Carregar assets Unity e associar materiais a efeitos | Permite programas próprios usando a estrutura gráfica do jogo. | Carregador ainda não comprovado; exige compatibilidade de assets, parâmetros, batches e descarregamento. |
| Renderização nativa própria | Alternativa se o carregamento de assets se mostrar inviável. | Maior dependência de OpenGL ES/Vulkan, contextos, sincronização e preservação do estado gráfico; entrada no pipeline ainda não verificada. |

Prioridade provisória: provar a reutilização de um efeito existente, investigar
o carregamento de assets e só então definir uma API para shaders próprios.
Não começar pela interceptação direta da API gráfica sem demonstrar a necessidade.

O custo de GPU depende da área desenhada, número de passes e amostras de
textura, não apenas da abordagem de carregamento. Alvos intermediários de
filtros de tela consomem memória proporcional à resolução, formato e quantidade.
Nenhum custo de tempo, memória ou FPS foi medido nesta pesquisa.

## Plano de investigação e critérios de conclusão

Todas as etapas abaixo estão **pendentes**, não são resultados de testes.

| Etapa | Pergunta | Evidência necessária para concluir |
|---|---|---|
| P1 — efeito nativo | Um mod consegue aplicar e remover um shader existente? | Nome real do shader/material, resultado visual comparado ao controle e ausência de substituição pelo Default. |
| P2 — carregador | Existe um caminho utilizável para carregar um asset externo de Shader? | Assinatura e thread verificadas; asset carregado como objeto válido; falha controlada para arquivo inexistente/incompatível. |
| P3 — shader próprio | Um programa que não acompanha o jogo participa do desenho? | Shader com nome exclusivo, origem externa identificada e efeito visual que o Default não produz. |
| P4 — parâmetros e sprites | Parâmetros e texturas do mod chegam ao material correto? | Alteração previsível de cor/intensidade/tempo, duas instâncias sem vazamento de estado, animação e transparência corretas. |
| P5 — filtro de tela | A captura existente aceita o efeito próprio? | Camadas afetadas registradas, ativação/desativação correta, resolução e zoom tratados. |
| P6 — convivência e ciclo de vida | Vários mods e reinicializações preservam o estado? | Ordem determinística de filtros, remoção dos recursos próprios, reinício e pausa/retomada sem efeitos residuais. |
| P7 — custo e compatibilidade | O efeito atende aos aparelhos escolhidos? | Backend gráfico real registrado; CPU/GPU e memória comparados ao controle em aparelho físico, além do emulador. |

No futuro teste, registrar especialmente:

- Dois mods usando o mesmo shader com parâmetros diferentes; nomes de registro
  devem evitar colisões e os materiais precisam de uma regra de propriedade.
- Ordem de filtros: aplicar A e depois B pode gerar resultado diferente de B
  e depois A. Não confundir essa ordem com a prioridade de músicas/fundos.
- Desativação de mod/bioma, troca de mundo, retorno ao menu e reinício do processo.
- Falta do asset, variante não suportada, índice de textura inválido e perda de
  referências aos objetos Unity/IL2CPP.
- Atlas, frames animados, recorte, rotação, flip, zoom e transparência.
- Configurações que desativam filtros, captura ou render targets.
- Separação entre a thread do jogo e a thread de renderização: o mecanismo
  existente para criar texturas na thread do jogo não autoriza comandos GL
  arbitrários nessa mesma thread. Consultar [threads](threads-e-motor-js.md).

Antes de estabelecer orçamento de desempenho, escolher aparelho, backend,
resolução e cena de controle. Não estimar FPS a partir do nome do shader.

## Referências locais para continuar

Endereços RVA da build identificada acima; consultar em `refs/dump.cs` e
resolver chamadas e strings com `refs/script.json`. Não copiar estes endereços
para outra versão sem nova verificação.

| Símbolo | RVA | Resultado desta pesquisa |
|---|---|---|
| `Effect.ApplyValuesToMaterial` | `0xEBB0F0` | Apenas retorno no código nativo. |
| `EffectPass..ctor(Effect, int, string, Material)` | `0xEBB884` | Recebe material Unity. |
| `PixelShader.LoadPass` | `0xEBD404` | Resources → Shader → Material → EffectPass; substituto Default. |
| `ScreenShader.get_Effect` | `0xEBFA24` | Registra os passes de tela candidatos. |
| `ScreenShader.LoadPass` | `0xEBFF44` | Mesmo mecanismo de carregamento, na família de tela. |
| `ScreenShader.ApplyValuesToMaterial` | `0xEC01AC` | Envia valores por SetColor/SetFloat/SetVector. |
| `ScreenShader..cctor` | `0xEC052C` | Registra IDs das propriedades da Unity. |
| `MaterialBuffer.GetMaterial` | `0xEC1B70` | Seleção de material e controle de instâncias; ciclo completo pendente. |
| `MaterialBuffer.MaterialPartition.GetMaterial` | `0xEE391C` | Associação de texturas Unity; investigar isolamento. |

Para reproduzir a inspeção inicial, com ferramentas já disponíveis no ambiente:

```powershell
Get-FileHash refs/libil2cpp.so, app/src/main/jniLibs/arm64-v8a/libil2cpp.so -Algorithm SHA256
Get-FileHash refs/libunity.so, app/src/main/jniLibs/arm64-v8a/libunity.so -Algorithm SHA256
Get-FileHash refs/global-metadata.dat, terraria1456_assets/src/main/assets/bin/Data/Managed/Metadata/global-metadata.dat -Algorithm SHA256
rg -n 'class EffectPass|class ScreenShader|class FilterManager|class AssetBundle' refs/dump.cs
```

O `llvm-objdump` do NDK permite conferir o corpo de `ScreenShader.LoadPass`:

```powershell
llvm-objdump --disassemble --no-show-raw-insn --start-address=0xEBFF44 --stop-address=0xEC01AC refs/libil2cpp.so
```

Esse comando pressupõe `llvm-objdump` no PATH. As chamadas precisam ser
correlacionadas com `ScriptMethod` em `refs/script.json`; os rótulos genéricos
que o disassembler imprime não são nomes confiáveis dos métodos do jogo.

## Fontes externas

Consultadas em 2026-10-03. Preferir documentação da versão usada pelo jogo.

- [tModLoader — FxcReader, branch 1.4.4](https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/ModLoader/Assets/Readers/FxcReader.cs): carregamento do efeito compilado; referência atual mais específica que exemplos antigos de XNB.
- [tModLoader — Expert Shader Guide](https://github.com/tModLoader/tModLoader/wiki/Expert-Shader-Guide): parâmetros, passes, shaders de sprites e tela. A seção de compilação contém exemplos de XNA/XNB; não assumir que toda a instrução se aplica ao fluxo atual de FXC.
- [tModLoader — ScreenShaderData](https://docs.tmodloader.net/docs/stable/class_screen_shader_data.html): referência para uma possível experiência de API, não implementação mobile.
- [Unity 2021.3 — AssetBundle bindings](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/AssetBundle/Managed/AssetBundle.bindings.cs): declarações das chamadas de carregamento; disponibilidade no jogo pendente.
- [Unity 2021.3 — AssetBundles](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundlesIntro.html): assets específicos de plataforma e carregamento em execução.
- [Unity 2021.3 — Building AssetBundles](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundles-Building.html): build target, compressão e manifests.
- [Unity 2021.3 — AssetBundle troubleshooting](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundles-Troubleshooting.html): seleção de variantes de shader ao construir bundles.
- [Unity — UUM-12895](https://issuetracker-mig.prd.it.unity3d.com/issues/assetbundles-dont-rebuild-in-older-versions-when-they-were-built-with-a-newer-version-of-unity): limites de compatibilidade com bundles construídos em versões mais novas, inclusive contexto de Unity 2021.3.
- [Unity 2021.3 — Shader.Find](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Shader.Find.html): pesquisa de shader existente e ausência de shaders removidos da build.
- [Unity 2021.3 — Native plugin interface](https://docs.unity3d.com/2021.3/Documentation/Manual/NativePluginInterface.html): callbacks na thread de renderização e contextos OpenGL. Disponibilidade dessa entrada no jogo não verificada.

## Registro das próximas rodadas

Ao atualizar este documento, registrar data, build, hashes, hipótese investigada,
método de inspeção/teste, resultado, localização das evidências e limitação.
Um teste negativo também deve permanecer registrado para evitar repetir uma
rota já descartada sem mudança de premissas.

Só promover uma etapa para validada em execução quando houver origem do shader
identificada, resultado comparado ao controle e dados suficientes para repetir
o experimento. Não substituir uma hipótese por uma promessa de implementação.
