# Catálogo da API JavaScript

O catálogo lista as classes publicadas em `script/js/mod/Exports.js`.
As assinaturas vêm de uma análise sintática com [Acorn](https://github.com/acornjs/acorn).
O gerador não executa os scripts do jogo nem usa mocks para descobrir a API.

## Arquivos publicados

- [Métodos por classe](../../docs/referencia/metodos.md): índice e uma tabela por classe.
- [Excel](../../docs/referencia/metodos.xlsx): abas Índice e Métodos, com filtros e cabeçalhos fixos.
- [CSV UTF-8](../../docs/referencia/metodos.csv): os mesmos registros em uma tabela.
- [JSON](../../docs/referencia/metodos.json): registros, classes, fontes e hash dos arquivos da API.

O inventário inclui métodos públicos de instância e estáticos, construtores
explícitos, getters/setters, herança entre as classes do módulo e os aliases
de NetWriter/NetReader. Métodos privados `#`, campos, enums, bases nativas
como `Error`, métodos de `Terraria.*` e funções de `bl.*` ficam fora dessa
contagem. Os objetos e enums exportados são listados separadamente no índice.

`Declarado` significa que existe uma declaração na API JavaScript. Esse
status não afirma cobertura de testes nem equivalência total ao tModLoader.
O retorno base é uma expressão do código, não um tipo inferido. Os métodos
descartados não recebem uma assinatura estimada.

## Atualização

Na raiz do repositório, com Node.js e npm:

```powershell
npm --prefix tools/docs ci
node tools/docs/generate.mjs
node tools/docs/generate.mjs --check
python tools/docs/verify.py
```

`generate.mjs` lê as assinaturas e suas linhas diretamente do código.
Extrai descrições das tabelas de `classes.md` e `modplayer-hooks.md`; quando
uma descrição específica não existe, usa o comentário adjacente da fonte
ou indica que a implementação deve ser consultada. Não infere tipos de
parâmetros nem transforma o nome de um método em uma descrição inventada.

`catalog.json` guarda as âncoras das referências, notas de compatibilidade
e as razões dos métodos descartados de ModNPC. Revise essas notas ao
mudar contratos. O gerador rejeita classes duplicadas, exports duplicados,
herança cíclica, linhas duplicadas, âncoras ausentes, notas sem método e
métodos presentes que estejam marcados como não implementados.

`--check` retorna erro quando Markdown, CSV ou JSON divergem da geração
atual. Execute-o depois de alterar a API ou a documentação dos contratos.
`verify.py` usa apenas a biblioteca padrão do Python. Confere todos os
registros do CSV e do Excel contra o JSON, as contagens do índice, a ausência
de fórmulas, os filtros e o congelamento dos cabeçalhos e colunas de identificação.

## Planilha

`workbook.mjs` usa `@oai/artifact-tool`, disponibilizado pelo runtime de
documentos do ambiente. O runtime não é uma dependência do aplicativo
Android nem do gerador de Markdown/CSV/JSON.

Prepare uma pasta de trabalho com `node_modules` apontando para as
dependências desse runtime. O parâmetro `--runtime` identifica essa pasta:

```powershell
node tools/docs/workbook.mjs --runtime build/api-docs --output-dir build/api-docs/outputs/catalogo
```

Use o executável Node.js do mesmo runtime. A planilha é criada na pasta
de saída e copiada para `docs/referencia/metodos.xlsx`. Os previews e os
relatórios de inspeção ficam apenas na pasta de build. Revise os previews
das duas abas e confira as linhas da tabela exportada contra `metodos.json`
antes de versionar o Excel. Ele não precisa de fórmulas nem de macros.
Acrescente `--preview` para renderizar também as duas abas. No runtime
Windows usado nesta atualização, essa opção salvou os previews e a planilha,
mas o Node encerrou com erro nativo. A geração sem renderização terminou
normalmente; `verify.py` conferiu integralmente o arquivo exportado.

Atualize a planilha junto com os três arquivos de texto. O hash em
`metodos.json` identifica as fontes das classes e de seus métodos herdados;
ele não é um identificador de resultado de testes no jogo.
