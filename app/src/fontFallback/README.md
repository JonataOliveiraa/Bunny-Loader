# Fonte reserva

O launcher e o menu de cheats usam a **Andy Bold**, a fonte do Terraria. Ela é
da Monotype e não vai para o Git: quem integra os binários do jogo põe também
`app/src/main/res/font/bunny.ttf`.

Sem esse arquivo, o build usa esta pasta: a **Comic Neue Bold**, da SIL Open
Font License 1.1 (texto em `OFL.txt`), com o mesmo nome de recurso
(`R.font.bunny`) e o mesmo caminho nos assets (`fonte/bunny.ttf`). Nada no código
muda; só a cara do texto.

Fonte: <https://github.com/crozynski/comicneue>
