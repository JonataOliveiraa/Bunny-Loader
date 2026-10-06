# Troca de idioma sem reiniciar

## Análise

O carregador aplicava os JSONs depois de `LoadLanguage` e `SetLanguage`.
Na versão móvel 301720, `LoadLanguage` chama `LoadFilesForCulture`, dispara
`OnLanguageChanging`, processa referências e prepara aliases antes de retornar.
O hook antigo chegava depois desses consumidores. As referências ao jogo nas
tabelas por cultura eram resolvidas com o idioma ativo durante a carga do mod.
Os tooltips não eram registrados para a próxima troca, e os rótulos de config
guardavam strings obtidas uma única vez.

Entradas: JSONs por cultura, mapas de nomes e descrições, cultura carregada e
chaves nativas. Saídas: valores atualizados nos mesmos `LocalizedText` e nos
tooltips processados. Casos cobertos: troca repetida, fallback inglês, referências
relativas e ao jogo, argumentos deslocados, referência ausente ou circular,
inicialização adiada, idioma repetido e texto literal.

## Projeto

| Abordagem | Tempo | Espaço | Manutenção |
|---|---|---|---|
| Recalcular textos a cada leitura ou tick | Repete resolução e passagem pela ponte | Temporários por leitura | Distribui a lógica entre consumidores |
| Aplicar após carregar os arquivos da cultura | Resolução na carga e na troca de idioma | Tabelas por cultura na carga, tabela resolvida do idioma atual | Um ponto de atualização antes dos eventos nativos |

A segunda abordagem usa um único hook em `LoadFilesForCulture`, depois do
original. Os JSONs continuam sendo lidos uma vez por mod. Os templates de conteúdo
resolvem referências entre textos do mod e preservam referências ao jogo até
carregar a cultura correspondente. `ModifyTooltipLines` continua recebendo as
linhas separadas e executando uma vez por cultura durante o registro.

Os tooltips registram a mesma chave usada pelo C++ e invalidam seu cache na troca.
Os rótulos, escolhas e textos de botões de config leem `LocalizedText.Value`;
o array de escolhas permanece o mesmo. Não há hook novo por tick ou frame.

Uma string obtida por `Translate` ou `GetTextValue` e guardada pelo próprio mod
continua sendo uma string. Para acompanhar o idioma, consulte no momento de uso
ou guarde o `LocalizedText` retornado por `GetText` e leia `.Value`.

## Execução

```powershell
node tools/tests/localization/check.mjs
```

O teste carrega o código de produção e simula a ordem confirmada na desmontagem
do jogo. A primeira execução reproduziu 29 falhas em 89 verificações. A suíte
foi ampliada para incluir registros durante os eventos; agora passam 95 de 95.
Os mocks verificam as assinaturas dos hooks contra `refs/dump.cs`.

O pacote em `manifest.json` e `content/` usa o UID
`c4c0f239-ad0e-4400-95f4-0354c884d12b`. Instale somente esse pacote para repetir
a prova nativa. Ele cria tipos de item, NPC, buff, prefixo e projétil para consultar os caches;
não cria entidades no mundo nem entrega itens. O teste troca entre português,
inglês e francês e restaura a cultura inicial. O log termina com
`localization FIM: tudo ok` quando todos os casos passam.

Referências circulares terminam no nome completo da chave que fechou o ciclo.
O resultado não conserva comandos `{$...}` para o processador nativo modificar
depois. O carregador acompanha a cultura dos arquivos mesmo durante
`OnLanguageChanging`, quando `Language.ActiveCulture` ainda aponta para a anterior.
Nomes de item e projétil sem entrada no JSON conservam o nome da classe.

## Fontes

- `refs/dump.cs` e desmontagem de `SetLanguage`, `LoadLanguage`,
  `LoadFilesForCulture` e `ItemTooltip.ValidateTooltip` da versão 301720.
- [LanguageManager.cs.patch do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/Localization/LanguageManager.cs.patch),
  que carrega traduções de mods ao final de `LoadFilesForCulture` e processa
  referências depois.
