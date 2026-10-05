# Resultado da revisão de desempenho

Medições em 5 de outubro de 2026 no QuickJS do APK debug, em MuMu, com o mesmo personagem, mundo e conjunto de mods. O cenário acrescenta oito ModPlayers com callbacks simples. A linha de base usa o código anterior às otimizações desta revisão.

## Medição nativa

| Cenário | Antes | Depois | Redução observada |
| --- | ---: | ---: | ---: |
| `CapAttackSpeeds_8`, mediana por chamada | 24,615 µs | 9,435 µs | 61,7% |
| `ItemCheck_veto_8`, mediana por chamada | 41,915 µs | 12,258 µs | 70,8% |
| Hook `JumpMovement`, custo JS por tick | 87,458 µs | 45,103 µs | 48,4% |

As medianas usam sete amostras de 20.000 chamadas. O perfil de hooks usa 300 ticks após o aquecimento. Tanto antes quanto depois foram contados **36.000 callbacks** das oito instâncias nesse intervalo.

No cenário sem ocultação ou reordenação, as camadas tiveram 11.280 passagens pelo JavaScript antes. Depois, tiveram 13.207 chamadas nativas e **zero passagens pelo JavaScript**. Houve 240 desenhos antes e 281 depois; a diferença é variação da renderização durante os 300 ticks. O filtro remove o custo JS dessas chamadas, mas conserva o caminho nativo.

O hook global de `Rectangle.Intersects` recebeu 19.766 chamadas na linha de base. Depois ele não foi instalado neste cenário, que só sobrescreve `ModifyHitNPCWithItem`. No teste multiplayer, que também sobrescreve colisão melee, o hook foi instalado e os ataques passaram.

Esses números medem os cenários descritos, não o quadro inteiro nem um ganho garantido de FPS. O [roteiro](README.md) explica o que cada medição inclui e suas limitações.

## Validação

- **44 verificações de comportamento** aprovadas, com 150 assinaturas nativas conferidas e quatro estágios nativos registrados.
- **102 verificações multiplayer** aprovadas após a correção da ponte: 48 no host, 50 no cliente e quatro de desconexão/reconexão. O cliente também confirmou a reconexão.
- Regressão nativa do filtro aprovada: `whileIn_multiplos_hooks=ok`.
- Sintaxe JavaScript, `git diff --check` e build debug aprovados.

Trechos da execução final:

```text
16:17:18.307 mpmodplayer host FIM falhas=0 casos=48 hooks=73
16:17:20.223 mpmodplayer cliente FIM falhas=0 casos=50 hooks=82
16:17:31.695 mpmodplayer host DESCONECTOU jogador=1
16:18:15.891 mpmodplayer host REJOIN FIM falhas=0
16:18:18.154 mpmodplayer cliente REJOIN FIM
16:20:01.336 modplayerperf whileIn_multiplos_hooks=ok
16:20:02.700 modplayerperf CapAttackSpeeds_8 mediana_us=9.435 minimo_us=9.162
16:20:04.449 modplayerperf ItemCheck_veto_8 mediana_us=12.258 minimo_us=11.670
16:20:09.474 modplayerperf FIM frames=300 callbacks=36000
```

O primeiro teste multiplayer com os novos filtros detectou que `ModifyShootStats` e `Shoot` eram ignorados. A ponte associava `whileIn` ao primeiro hook do método, filtrado para itens específicos, enquanto o hook de ModPlayer estava ativo em outro slot. O contador compartilhado por método e thread corrigiu esse caso; a sessão completa foi repetida com sucesso.

Os pacotes temporários foram removidos, as configurações de entrada foram restauradas e a segunda instância foi encerrada. O código continua mantendo a frequência original dos callbacks e a detecção de desconexão a cada atualização local.
