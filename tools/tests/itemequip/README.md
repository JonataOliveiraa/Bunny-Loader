# Estado de ModItem ao transferir equipamentos

O fixture chama os metodos nativos de transferencia da interface movel e
verifica dono, contador, dados mutaveis e o vinculo `ModItem.Item`. O item
implementa SaveData/LoadData e NetSend/NetReceive, mas os testes alteram seu
estado em memoria sem salvar antes de transferir.

Casos nativos:

- Arraste (`ItemSlot.LeftClick`) para acessorio vazio.
- Troca entre dois tipos diferentes de acessorio, com donos diferentes.
- Retorno de excedente ao inventario durante o arraste. Este caso forca uma
  pilha de dois acessorios para exercitar o ramo de transferencia parcial;
  nao representa a configuracao normal do Valentine Ring.
- `ItemSlot.PlaceItem` em slot vazio, com Clone customizado e dados independentes.
- Vinte transferencias consecutivas, sem misturar donos.
- Transferencia parcial com `keepStack`, preservando origem e destino.
- `SetDefaults` fora da transferencia continua reiniciando o estado.

Use o manifesto e a pasta `content` como um mod de teste, habilitado sozinho
em copias de personagem e mundo. A rodada roda uma vez apos entrar no mundo
e restaura os slots e campos de interface alterados antes do proximo frame.
Procure `itemequip FIM checks=7 falhas=0` no log. O fixture testa metodos
nativos; nao automatiza gestos fisicos na tela.

Em 8 de outubro de 2026, no MuMu com jogo 301720, a mesma rodada apresentou
quatro falhas no APK anterior e zero no APK compilado com a correcao. O
arraste simples e a troca completa ja passavam antes; o caminho PlaceItem
recriava o item somente pelo tipo e perdia o estado do mod. Logs locais:
`build/itemequip/before.log`, `after.log`, `unit.log` e `gradle.log`.

Testes JS complementares:

```powershell
node tools/tests/moditemdata/check.mjs
```

Os 67 casos da suite passaram. A cobertura adicionada inclui itens sem
hooks de persistencia, Clone customizado, transferencia aninhada, excecao
nativa, criacao de item dentro do Clone, dados em cache e falha anterior de
LoadData. As chamadas nativas dessa suite JS sao simuladas; o fixture deste
diretorio valida o caminho de transferencia dentro do jogo.
