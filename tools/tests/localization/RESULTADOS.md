# Resultados de localização

Execução em 6 de outubro de 2026, na versão móvel 301720.

| Validação | Resultado |
|---|---|
| Código de produção carregado em Node | 95/95 verificações |
| APK real no MuMu, após a montagem do conteúdo no menu | 22/22 verificações; `localization FIM: tudo ok` |
| Regressão ModNPC | 46 verificações; 22 assinaturas nativas |
| Regressão ModPlayer | 44 verificações; 150 assinaturas nativas |
| Regressão GlobalProjectile | 59 verificações; 22 assinaturas nativas |
| Regressão de morte de projéteis | 16 verificações |
| Catálogo de métodos | Markdown e JSON atualizados; CSV e Excel conferidos |
| Build e assinatura | `assembleDebug` concluído; assinatura APK v2 válida |

A primeira suíte reproduziu 29 falhas em 89 verificações antes da correção.
O teste nativo encontrou uma interação com o processador de referências
circulares e confirmou a perda dos nomes de item e projétil sem JSON ao
trocar para francês. Ambos os casos foram corrigidos e reexecutados.

No APK, foram consultados os caches reais de nomes de item, NPC, buff e
prefixo, a descrição do buff e as linhas processadas do tooltip. As trocas
entre português, inglês e francês verificaram também o fallback e a volta
à cultura inicial. O mesmo `LocalizedText` guardado antes da troca recebeu
o valor novo. Categorias, variantes e referências relativas, completas,
ausentes, circulares e com deslocamento de argumentos passaram.

Os rótulos, opções e textos de botão de config foram validados pela suíte
Node com o código de produção. A prova nativa consulta caches; a aparência
visual da tela de configuração não foi verificada nessa execução.

Os JSONs foram lidos uma vez por mod. A troca de idioma utiliza um único
hook em `LoadFilesForCulture`, após o original, sem hook por tick.

APK gerado: `out/bunny-loader.apk`, 204422286 bytes.

SHA-256: `cd703eb82ebef4044bfa2ebd09d7aa30bef3ea41987dced2b091ce35f3afa32c`.
