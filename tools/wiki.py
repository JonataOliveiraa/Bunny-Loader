#!/usr/bin/env python3
"""Gera a wiki do GitHub a partir do README.md e de docs/.

Uso: python tools/wiki.py <pasta do clone da wiki>
     (ex.: git clone https://github.com/JonataOliveiraa/Bunny-Loader.wiki.git)

A fonte continua sendo docs/: a wiki é só uma cópia com os links trocados, e
o workflow .github/workflows/wiki.yml a regenera a cada push na main. Por isso
o script apaga tudo que há na pasta de saída (menos o .git) antes de escrever:
página editada direto na wiki some no próximo push.

  - cada .md vira uma página com nome próprio (a wiki não tem pastas, e há
    quatro README.md);
  - link para outro .md vira link para a página, com a âncora;
  - imagem e qualquer outro arquivo do repositório (samples/, tools/, os
    dados do histórico) vira link para o GitHub, na main;
  - blocos e trechos de código ficam como estão.
"""
import os
import re
import shutil
import sys
import unicodedata
from urllib.parse import quote

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = "JonataOliveiraa/Bunny-Loader"
BRANCH = "main"
BLOB = f"https://github.com/{REPO}/blob/{BRANCH}/"
TREE = f"https://github.com/{REPO}/tree/{BRANCH}/"
RAW = f"https://raw.githubusercontent.com/{REPO}/{BRANCH}/"

# arquivo do repositório -> nome da página. Um .md novo em docs/ precisa
# entrar aqui (e no SIDEBAR), senão o script para com erro.
PAGES = {
    "README.md": "Home",
    "docs/README.md": "Documentação",
    "docs/mods/README.md": "Criando-mods",
    "docs/mods/00-como-funciona.md": "Como-funciona",
    "docs/mods/01-hooks-do-zero.md": "Hooks-do-zero",
    "docs/mods/02-ref-e-out.md": "ref-e-out",
    "docs/mods/03-custo-e-desempenho.md": "Custo-e-desempenho",
    "docs/mods/04-conteudo-novo.md": "Conteúdo-novo",
    "docs/mods/05-itens.md": "Itens",
    "docs/mods/06-projeteis.md": "Projéteis",
    "docs/mods/07-npcs.md": "NPCs",
    "docs/mods/08-jogador-e-buffs.md": "Jogador-e-buffs",
    "docs/mods/09-blocos.md": "Blocos",
    "docs/mods/10-sons-e-musica.md": "Sons-e-música",
    "docs/mods/11-conversa-entre-mods.md": "Conversa-entre-mods",
    "docs/mods/12-globais-e-mundo.md": "Globais-e-mundo",
    "docs/referencia/classes.md": "Referência-das-classes",
    "docs/referencia/ponte-e-bl.md": "Referência-da-ponte-e-bl",
    "docs/nucleo/README.md": "Núcleo",
    "docs/nucleo/hooks.md": "Núcleo-hooks",
    "docs/nucleo/threads-e-motor-js.md": "Núcleo-threads-e-motor-JS",
    "docs/nucleo/ponte.md": "Núcleo-ponte",
    "docs/nucleo/conteudo.md": "Núcleo-conteúdo",
    "docs/historico/README.md": "Histórico",
    "docs/historico/UNITY-HOSTING.md": "Histórico-Unity-hosting",
    "docs/historico/DECISAO-ARQUITETURA.md": "Histórico-decisão-de-arquitetura",
    "docs/historico/AVALIACAO-PONTE-E-CRASH.md": "Histórico-avaliação-da-ponte-e-crash",
    "docs/historico/PONTE-OTIMIZACAO.md": "Histórico-otimização-da-ponte",
}

SIDEBAR = [
    ("**[Início](Home)** · [Documentação](Documentação)", None),
    ("Criando mods", [
        ("Criando-mods", "Visão geral"),
        ("Como-funciona", "0. Como funciona"),
        ("Hooks-do-zero", "1. Hooks"),
        ("ref-e-out", "2. `ref` e `out`"),
        ("Custo-e-desempenho", "3. Custo e desempenho"),
        ("Conteúdo-novo", "4. Conteúdo novo"),
        ("Itens", "5. Itens"),
        ("Projéteis", "6. Projéteis"),
        ("NPCs", "7. NPCs"),
        ("Jogador-e-buffs", "8. Jogador e buffs"),
        ("Blocos", "9. Blocos"),
        ("Sons-e-música", "10. Sons e música"),
        ("Conversa-entre-mods", "11. Conversa entre mods"),
        ("Globais-e-mundo", "12. Globais e o mundo"),
    ]),
    ("Referência", [
        ("Referência-das-classes", "O que cada classe tem hoje"),
        ("Referência-da-ponte-e-bl", "A ponte e o `bl`"),
    ]),
    ("O núcleo nativo", [
        ("Núcleo", "Visão geral e boot"),
        ("Núcleo-hooks", "Hooks por dentro"),
        ("Núcleo-threads-e-motor-JS", "Threads e o motor JS"),
        ("Núcleo-ponte", "A ponte JS ↔ jogo"),
        ("Núcleo-conteúdo", "Conteúdo novo por dentro"),
    ]),
    ("Histórico", [
        ("Histórico", "Índice"),
        ("Histórico-Unity-hosting", "Unity hosting"),
        ("Histórico-decisão-de-arquitetura", "Decisão de arquitetura"),
        ("Histórico-avaliação-da-ponte-e-crash", "Avaliação da ponte e crash"),
        ("Histórico-otimização-da-ponte", "Otimização da ponte"),
    ]),
]

FENCE = re.compile(r"^(```|~~~)")
CODE_SPAN = re.compile(r"(`+)(?:.|\n)*?(?<!`)\1(?!`)")
LINK_TARGET = re.compile(r"\]\(([^)\s]+)((?:\s+\"[^\"]*\")?)\)")
HTML_SRC = re.compile(r"""(\s(?:src|href)=")([^"]+)(")""")


def page_url(name):
    return quote(name)


def repo_url(path, image):
    if image:
        return RAW + quote(path)
    full = os.path.join(ROOT, path)
    return (TREE if os.path.isdir(full) else BLOB) + quote(path)


class Converter:
    def __init__(self):
        self.errors = []
        self.links = []  # (página de origem, página, âncora)

    def target(self, src, target):
        if re.match(r"^[a-z][a-z0-9+.-]*:", target, re.I) or target.startswith("#"):
            return target
        path, _, anchor = target.partition("#")
        rel = os.path.normpath(os.path.join(os.path.dirname(src), path)).replace(os.sep, "/")
        if rel.startswith(".."):
            self.errors.append(f"{src}: link para fora do repositório: {target}")
            return target
        if not os.path.exists(os.path.join(ROOT, rel)):
            self.errors.append(f"{src}: link quebrado: {target}")
            return target
        if rel in PAGES:
            self.links.append((src, PAGES[rel], anchor))
            return page_url(PAGES[rel]) + ("#" + anchor if anchor else "")
        if rel.endswith(".md") and rel.startswith("docs/"):
            self.errors.append(f"{src}: {rel} não está em PAGES")
        image = rel.lower().endswith((".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"))
        return repo_url(rel, image) + ("#" + anchor if anchor else "")

    def prose(self, src, text):
        """Troca os links de um trecho sem código cercado, poupando `código`."""
        spans = [m.span() for m in CODE_SPAN.finditer(text)]

        def in_code(pos):
            return any(a <= pos < b for a, b in spans)

        def link(m):
            if in_code(m.start()):
                return m.group(0)
            return "](" + self.target(src, m.group(1)) + m.group(2) + ")"

        def html(m):
            if in_code(m.start()):
                return m.group(0)
            return m.group(1) + self.target(src, m.group(2)) + m.group(3)

        # as duas trocas não mudam posições relevantes uma para a outra:
        # recalcula os trechos de código depois da primeira.
        text = LINK_TARGET.sub(link, text)
        spans = [m.span() for m in CODE_SPAN.finditer(text)]
        return HTML_SRC.sub(html, text)

    def convert(self, src):
        with open(os.path.join(ROOT, src), encoding="utf-8") as f:
            lines = f.read().split("\n")
        out, chunk, fence = [], [], None
        for line in lines:
            m = FENCE.match(line.lstrip())
            if fence is None:
                if m:
                    out.append(self.prose(src, "\n".join(chunk)))
                    chunk = []
                    fence = m.group(1)
                    out.append(line)
                else:
                    chunk.append(line)
            else:
                out.append(line)
                if line.strip().startswith(fence) and line.strip().strip(fence[0]) == "":
                    fence = None
        out.append(self.prose(src, "\n".join(chunk)))
        body = "\n".join(out).rstrip("\n")
        note = (f"\n\n---\n\n<sub>Página gerada de [`{src}`]({BLOB}{quote(src)}). "
                f"Edite lá: a wiki é regenerada a cada push na `main`.</sub>\n")
        return body + note


def slug(heading):
    """Âncora do GitHub para um título."""
    s = re.sub(r"<[^>]+>", "", heading).strip().lower()
    s = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", s)
    s = "".join(c for c in s if c in " -_" or unicodedata.category(c)[0] in "LN")
    return s.replace(" ", "-")


def anchors(src):
    found, counts, fence = set(), {}, False
    with open(os.path.join(ROOT, src), encoding="utf-8") as f:
        for line in f:
            if FENCE.match(line.lstrip()):
                fence = not fence
            elif not fence and line.startswith("#"):
                base = slug(line.lstrip("#"))
                n = counts.get(base, 0)
                counts[base] = n + 1
                found.add(base if n == 0 else f"{base}-{n}")
    return found


def sidebar():
    lines = []
    for title, items in SIDEBAR:
        if items is None:
            lines += [title, ""]
            continue
        lines += [f"**{title}**", ""]
        lines += [f"- [{label}]({page_url(page)})" for page, label in items]
        lines.append("")
    return "\n".join(lines)


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    dest = sys.argv[1]
    if not os.path.isdir(os.path.join(dest, ".git")):
        sys.exit(f"{dest} não é um clone da wiki (falta o .git)")

    docs = {os.path.relpath(os.path.join(d, f), ROOT).replace(os.sep, "/")
            for d, _, fs in os.walk(os.path.join(ROOT, "docs")) for f in fs if f.endswith(".md")}
    conv = Converter()
    for missing in sorted(docs - PAGES.keys()):
        conv.errors.append(f"{missing} não está em PAGES")
    in_sidebar = {p for _, items in SIDEBAR if items for p, _ in items} | {"Home", "Documentação"}
    for page in sorted(set(PAGES.values()) - in_sidebar):
        conv.errors.append(f"página {page} não está no SIDEBAR")

    pages = {PAGES[src]: conv.convert(src) for src in PAGES}
    by_page = {page: src for src, page in PAGES.items()}
    for src, page, anchor in conv.links:
        if anchor and anchor not in anchors(by_page[page]):
            conv.errors.append(f"{src}: âncora inexistente: {page}#{anchor}")
    if conv.errors:
        sys.exit("\n".join(conv.errors))

    for name in os.listdir(dest):
        if name == ".git":
            continue
        full = os.path.join(dest, name)
        shutil.rmtree(full) if os.path.isdir(full) else os.remove(full)
    for page, text in pages.items():
        with open(os.path.join(dest, page + ".md"), "w", encoding="utf-8") as f:
            f.write(text)
    with open(os.path.join(dest, "_Sidebar.md"), "w", encoding="utf-8") as f:
        f.write(sidebar())
    print(f"{len(pages)} páginas em {dest}")


if __name__ == "__main__":
    main()
