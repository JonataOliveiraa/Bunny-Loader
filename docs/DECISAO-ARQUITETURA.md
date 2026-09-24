# Decisão de arquitetura: como colocar o Bunny Loader dentro do Terraria

Status: **em aberto**. Escrito depois que a Fase 1 esbarrou no PairIP.
Referência técnica: [UNITY-HOSTING.md](UNITY-HOSTING.md).

---

## 1. O problema, medido

O objetivo do projeto nunca foi hospedar a `UnityPlayer` — foi **colocar a
`libbunny.so` dentro do processo do jogo** para hookar métodos gerados pelo
IL2CPP. Hospedar era só o meio escolhido.

Esse meio bateu num muro. O que está comprovado no dispositivo:

| Fato | Evidência |
|---|---|
| O APK usa PairIP | `application:name = com.pairip.application.Application` |
| As constantes de string do app são criptografadas | `uk.co.drstudios.lvl.yh.sHQPfQqKk`: 13 campos `static String`, **zero métodos** |
| Sem elas, a Unity quebra | `getNaturalOrientation` lê `sHQPfQqKk.pPb` (deveria ser `"window"`) → `getSystemService(null)` → NPE |
| Acionar o `StartupLauncher` direto não resolve | Sonda antes e depois: `null`. Tudo "ok", nada populado, no mesmo milissegundo |
| Porque o `invoke` do dex é um stub | `VMRunner.invoke` começa com `const/4 v0, 0; return-object v0`; corpo real no offset 0002, inalcançável |

A conclusão: **a `libpairipcore.so` reescreve esse método em memória, e só faz
isso quando inicializada pelo caminho de boot legítimo do app.** Carregar a lib
não basta.

---

## 2. Os caminhos

### A. Hospedar no nosso processo (o desenho atual)

Continuar como está e fazer o nosso processo passar por legítimo para a
`libpairipcore.so`.

O que falta descobrir: o que exatamente ela verifica. Candidatos prováveis são
nome do pacote, assinatura do APK e se o `Application` real rodou. Cada um
desses é um item a contornar, e nenhum está mapeado ainda.

- **Root:** não precisa
- **Distribuição:** a mais limpa — nosso APK sozinho, usuário usa a cópia dele
- **Fragilidade:** alta. É uma corrida armamentista contra uma proteção que a
  Google atualiza; cada update do jogo pode quebrar
- **Esforço:** indeterminado, e esse é o problema — pode ser uma tarde ou pode
  não ter fim

### B. Lançar o jogo e injetar no processo dele

O launcher inicia o Terraria normalmente. O `Application` dele roda, o PairIP
inicializa, as strings se populam, tudo funciona como projetado. Aí a gente
injeta a `libbunny.so` nesse processo.

O problema da camada Java **desaparece por completo** — a gente deixa de
disputar com o PairIP e passa a conviver com ele.

- **Root:** **sim**, para injeção em processo alheio (ptrace, ou módulo estilo
  Zygisk)
- **Distribuição:** o APK é livre, mas só funciona em aparelho rooteado. Para
  Terraria mobile isso corta a maioria esmagadora do público
- **Fragilidade:** baixa no lado Java. A parte nativa (offsets) já é resolvida
  em runtime e validada pelo `GameRefs`
- **Esforço:** moderado e previsível

### C. Repackaging do APK

**Atenção a um mal-entendido:** repackaging **não é remover o PairIP**. As
strings estão criptografadas e só a `libpairipcore.so` as decifra — sem PairIP
o app não funciona. Repackaging significa **manter o PairIP intacto** e apenas
acrescentar a nossa `libbunny.so` mais um gancho que a carregue.

Mas ao reassinar o APK, o `SignatureCheck` (que compara com a assinatura
embutida) passa a falhar. Ele vive em `Application.attachBaseContext` e **não**
está protegido por VM, então dá para neutralizar. Fica o desconhecido de se a
`libpairipcore.so` faz a própria verificação por dentro.

- **Root:** não precisa
- **Distribuição:** aqui está o ponto delicado. O APK modificado é uma cópia
  alterada de um jogo pago — **não pode ser distribuído**. O que se distribui é
  a *ferramenta* que faz o patch, e cada usuário aplica na cópia dele. Isso é
  viável e é um padrão conhecido, mas muda o produto: o Bunny Loader deixaria de
  ser só um app e passaria a ter um patcher
- **Efeitos colaterais:** assinatura diferente da Play Store (perde updates
  automáticos, pode conflitar com a instalação original), e o Play Integrity
  quebra — irrelevante offline, relevante se o jogo checar algo online
- **Fragilidade:** média. Refazer o patch a cada update do jogo
- **Esforço:** alto

### D. Desenvolver primeiro contra uma versão sem PairIP

Não é um caminho final, é uma forma de reduzir risco. Versões mais antigas do
Terraria mobile são anteriores ao PairIP. Rodar o pipeline inteiro nelas
(Fases 1 a 4) prova a arquitetura de ponta a ponta, e o PairIP vira um problema
isolado, atacado depois com o resto já funcionando.

- **Custo:** um dump a mais, e os offsets mudam entre versões (o `GameRefs` já
  recusa iniciar se algo não resolver, então isso é detectado, não silencioso)
- **Risco:** resolver o fácil e descobrir tarde que o difícil não tem solução

---

## 3. Comparação

| | A. Hospedar | B. Lançar + injetar | C. Repackaging | D. Versão antiga |
|---|---|---|---|---|
| Precisa root | não | **sim** | não | não |
| Público alcançável | todos | só rooteados | todos | todos |
| Distribui o quê | só o app | só o app | app + patcher | só o app |
| Redistribui o jogo | não | não | **não** (patch local) | não |
| Esforço | indeterminado | moderado | alto | baixo |
| Fragilidade por update | alta | baixa | média | — |
| Bloqueio conhecido | sim | nenhum | assinatura | nenhum |

---

## 4. O que vale independente da escolha

Nada do que foi feito se perde:

- **Fase 0 inteira** — `tools/dump.sh`, `tools/dumpgrep.sh`, o dump validado,
  os offsets conferidos. Isso sempre foi sobre o lado nativo
- **`GameRefs`** e a disciplina de resolver tudo em runtime com validação
- **O núcleo nativo** (`libbunny.so`): LibWatcher, HookManager, Il2CppApi,
  ScriptEngine. Todos operam dentro do processo do jogo, não importa como a
  gente chegou lá
- **O diagnóstico** — sem ele a gente ainda estaria olhando para `null`

O que é específico do caminho A é a camada de hosting: `GameEnvironment`,
`UnityHost`, os overrides de Context na `GameActivity`, o `PairipBootstrap`.

---

## 5. O que eu não sei

Honestidade sobre os limites do que está estabelecido:

- **Como outros launchers fazem.** Existem launchers que rodam sem root e
  carregam mods no Terraria. Não investiguei, e não vou chutar
- **O que a `libpairipcore.so` verifica.** Sem isso, o esforço do caminho A é
  um número desconhecido
- **Se ela faz verificação própria de assinatura**, o que afeta o caminho C
- **Se versões antigas do Terraria realmente não têm PairIP** — plausível pela
  época, não conferido

---

## 6. Recomendação

**B para desbloquear agora, com C como caminho de distribuição depois.**

O raciocínio: o valor do projeto está no núcleo nativo e na API de mods, e nada
disso foi testado ainda. O caminho B chega lá rápido e com risco previsível,
porque para de brigar com o PairIP. Com root no MuMu, dá para percorrer as
Fases 2, 3 e 4 inteiras e ter mods JS rodando.

Só então a pergunta de distribuição fica madura — e aí o caminho C resolve o
alcance, com o núcleo já provado e a única incógnita sendo a assinatura.

O caminho A é o único que entrega tudo de uma vez, e é justamente por isso que
desconfio dele: o esforço é indeterminado e a manutenção é uma corrida contra
atualizações que não controlamos.

**Se o alcance sem root for requisito inegociável do produto**, então a ordem
inverte e C vira o caminho principal desde já — vale dizer isso agora, porque
muda o que construir em seguida.

---

# DECISAO TOMADA: caminho B (lançar + injetar)

Escolhido para desbloquear as Fases 2-4. Distribuição sem root fica para depois
(provavelmente caminho C).

## Mecanismo de injeção: propriedade `wrap.<pacote>`, não ptrace

Medido no MuMu (device emulator-5554):

| Item | Valor |
|---|---|
| SELinux | Permissive |
| root (`su`) | funciona |
| `ro.debuggable` | 0 (build user) |
| Magisk/Zygisk | ausente |
| `setprop wrap.com.and.games505.TerrariaPaid ...` | aceita e lê de volta |

`wrap.<pacote>` é um recurso de startup padrão do Android (o mesmo do `wrap.sh`
de profiling/debug). Com ele o Zygote lança o app sob um wrapper, o que permite
`LD_PRELOAD` da `libbunny.so` no processo do próprio jogo.

Por que não ptrace: sob a tradução ARM→x86 do MuMu, mexer em processo por
ptrace é frágil (mesma razão pela qual os hooks nativos precisam de ARM real).
O `wrap.` opera no nível de startup do Zygote, não instrução a instrução, então
não depende do tradutor.

## O que isso muda na base já escrita

- **Sai** a camada de hosting: `GameEnvironment`, `UnityHost`, os overrides de
  Context na `GameActivity`, o `PairipBootstrap`. Não os apago ainda — ficam de
  referência até o novo caminho estar de pé
- **Fica** tudo do núcleo nativo e da Fase 0
- **Entra:** o launcher passa a (1) setar a `wrap.` via root, (2) iniciar a
  Activity real do jogo, (3) o `LibWatcher` já registra o hook pendente de
  `il2cpp_init` assim que a `libbunny.so` for pré-carregada

## Sequência nova de boot

```
LauncherActivity (nosso processo, sem root pra UI)
  └─ "Jogar"
       ├─ via root: setprop wrap.<pkg> = LD_PRELOAD=.../libbunny.so
       ├─ via root: am start com.and.games505.TerrariaPaid/.UnityPlayerActivity
       └─ Zygote sobe o jogo com a libbunny.so pré-carregada
             ├─ Application do jogo roda → PairIP inicializa → strings OK
             ├─ constructor da libbunny (ou JNI_OnLoad) → LibWatcher
             │     └─ hook pendente em il2cpp_init (ShadowHook)
             └─ Unity carrega libil2cpp.so → il2cpp_init → runtime::boot()
```

O ponto elegante: a `libbunny.so` é carregada **antes** da `libil2cpp.so`
porque o `LD_PRELOAD` acontece no exec. O hook pendente do ShadowHook, que já
está no código, era feito exatamente pra isso.

## Pendências desta virada

- Onde a `libbunny.so` roda o bootstrap: precisa de um ponto de entrada no
  load da lib (constructor `__attribute__((constructor))` ou `JNI_OnLoad`), já
  que não há mais o `NativeBridge.init` do Java chamando `installWatcher`
- Como passar a config (modsDir, etc.) para um processo que não é o nosso:
  provavelmente um arquivo em local acordado (ex: `/data/local/tmp/bunny/`) que
  a lib lê no load
- Copiar a `libbunny.so` para um caminho que o processo do jogo consiga ler

---

# RESULTADO: repackage funciona; hook precisa de ARM real

Medido no MuMu com o APK repackaged (libmain.so com NEEDED libbunny.so):

```
BunnyLoader: libbunny carregada no processo do jogo (config=sim, versao alvo 301543)
BunnyLoader: Falha ao registrar hook de il2cpp_init: Linker with an unsupported architecture
IL2CPP: JNI_OnLoad
Unity: 2021.3.56f2 ... CPU 'arm64-v8a' ... Backend 'il2cpp'
IL2CPP: Locale pt-PT
shadowhook_tag: shadowhook init(mode: UNIQUE), return: 0, real-init: yes
```

No processo do jogo: libbunny (3), libshadowhook (3), libil2cpp (6) mapeadas;
jogo chega ao menu.

## Provado
- Injecao por repackage (DT_NEEDED em libmain.so) coloca a nossa lib no
  processo do jogo — nosso constructor roda.
- PairIP contornado: rodar dentro do processo do jogo faz as strings serem
  decifradas normalmente. O re-sign com chave de dev NAO abortou o boot
  (nenhum SignatureTamperedException). Ou seja, neste build a checagem de
  assinatura do PairIP nao derruba o app repackaged.
- Nenhum dex alterado.

## Bloqueio restante (do emulador, nao do design)
`shadowhook_init` retorna 0, mas registrar o hook falha com "Linker with an
unsupported architecture". Sob o houdini (tradução ARM->x86 do MuMu), o
ShadowHook nao reconhece o linker para fazer o inline hook. Este e exatamente
o alerta repetido desde a Fase 0: **hooks nativos precisam de ARM real.**

## Proximo passo
A camada de injecao esta pronta e reproduzivel (tools/repack.py). O motor de
hook so pode ser validado em ARM de verdade — aparelho Android ARM (idealmente
com root/Magisk para voltar ao caminho B limpo, mas o repackage tambem roda sem
root) ou um host ARM. Tudo ate o il2cpp_init esta feito no emulador.

---

# FASE 3a VALIDADA no emulador: camada de resolucao funciona

A sonda (runtime/Probe.cpp) rodou no processo do jogo e resolveu tudo por nome,
com os offsets batendo com o dump 1.4.5.6.4:

```
sonda: il2cpp pronto; carregando API
GameRefs ok | Entity.velocity=0x1C (esp 0x1C) Projectile.type=0x64 (esp 0x64) active=0x49 (esp 0x49)
sonda: RESOLUCAO OK — camada de bind validada no processo do jogo
```

Provado sob houdini (emulador):
- Chamadas a API do IL2CPP (il2cpp_*) funcionam traduzidas.
- Api::load() acha Assembly-CSharp.dll + mscorlib.dll.
- resolveGameRefs() resolve classes/campos/metodos -> offsets corretos.

Detalhe critico: chamar il2cpp_domain_get() DURANTE o il2cpp_init crasha
(SIGSEGV). A sonda espera o jogo assentar (10s) antes de tocar na API. O
caminho do hook (ARM) nao tem esse problema porque so roda depois do init.

Falta so o inline hook, que e ARM-only. Ordem de trabalho daqui:
- ARM real: instalar o watcher (ShadowHook) e rodar runtime::boot() de verdade
  -> Projectile.AI/SetDefaults hookados -> Fase 4 (QuickJS).
- No emulador: dá para adiantar tudo que nao depende do hook — os bindings
  QuickJS, o registro de conteudo, e ler/escrever estado do jogo via a sonda.

---

# CORRECAO: hook FUNCIONA no emulador (por endereco)

A observacao do usuario (outro launcher de mods roda no MuMu) estava certa e derrubou a
conclusao anterior de "hooks so em ARM real". Medido:

```
hooktest: Projectile.SetDefaults hookado (por endereco)
hooktest: Main.DoUpdate hookado — aguardando disparo
>>> HOOK Main.DoUpdate DISPAROU (frame 0) — hook funciona sob houdini!
```

O jogo segue vivo, nosso codigo roda todo frame e chama o original.

## O que realmente falha sob houdini
So o modo PENDENTE por nome: `shadowhook_hook_sym_name("libil2cpp.so", ...)`,
que inspeciona o linker x86 -> erro 35. O `shadowhook_init` em si passa.

## O que funciona sob houdini
`shadowhook_hook_func_addr` num metodo JA carregado: reescreve o prologo da
funcao ARM (o houdini traduz), sem tocar no linker. Como o inline hook patcha o
prologo do alvo, TODOS os chamadores sao redirecionados — inclusive as chamadas
diretas compiladas pelo AOT (nao e swap de ponteiro).

## Consequencia no design
Nao precisamos hookar il2cpp_init. O gatilho de boot passa a ser "esperar o
il2cpp ficar pronto" (como a sonda ja faz) e entao instalar os hooks de runtime
por endereco. O mesmo codigo roda no emulador E em ARM real. O caminho A/B/C de
distribuicao continua valendo; o desenvolvimento inteiro (Fases 3 e 4) fica
desbloqueado no MuMu, sem root e sem ARM.
