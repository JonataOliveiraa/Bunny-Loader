# Templo Pirâmide

Mod JavaScript para o Bunny Loader, Terraria Mobile 1.4.5.6 (build 301720).

`content/temple-plan.js` gera a planta sem acessar o jogo. `content/temple-runtime.js` aplica os tiles e atualiza `GenVars` para a etapa `templePart2` do Terraria. `content/main.js` registra o hook na carga do mod.

Instalação e comportamento: [description.md](description.md).

Para testar a geometria e a integração com uma ponte simulada:

```powershell
node tools/temple/check.mjs
```

Para atualizar o script independente do Editor e gerar o ZIP importável:

```powershell
python tools/temple/package.py
```

Saídas: `tools/editor/templo.js` e `out/TemploPiramide.zip`. O script do Editor instala um único hook por processo; executá-lo novamente atualiza o gerador. Execute-o antes de criar um mundo, sem fechar o jogo. O script mantém os nomes globais `buildTemple` e `templeHooked` da versão anterior para permitir a atualização na mesma sessão.

Para renderizar a planta testada e atualizar o ícone (Python com Pillow):

```powershell
node tools/temple/check.mjs
python tools/temple/preview.py
python tools/temple/package.py
```

A prévia gerada pelos testes mostra a planta com os locais de baús, placas e emissores planejados, antes da decoração adicional do jogo. Os testes locais não substituem a validação de criação de mundo e combate ao Golem no Android.

O gerador nunca chama o callback original de `makeTemple`, nem após falhas. A exceção é contida porque a ponte do Bunny Loader executa o original automaticamente quando o callback lança.

Referências conferidas no fonte local fornecido pelo usuário (`TerrariaSource/Terraria`): `WorldGen.makeTemple`, `WorldGen.PlaceDoor`, `WorldGen.AddLihzahrdAltar`, `WorldGen.templePart2`, `WorldGen.mayanTrap`, `WorldGen.AddBuriedChest`, `WorldGen.AddWireFromPointToPoint`, `Chest.NearOtherChests` e `Wiring`. As assinaturas chamadas pelo mod também foram conferidas no dump da versão Mobile. O código-fonte do Terraria não é incluído no pacote.
