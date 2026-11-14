#!/usr/bin/env python3
"""Prepares the wiki clone before a push.

Usage: python tools/wiki/build.py [--strict] [wiki folder]   (default: ./wiki)

- Each `<!-- shot ... -->` marker followed by a fenced code block becomes a code
  image (images/code/<name>.png) plus the same code, ready to copy, in a
  collapsed block. Attributes: name, title, hl ("3,5-7"), lang, alt, numbers=no.
- Pages listed in _Sidebar.md get the section name on top and links to the
  previous and next page at the bottom, in sidebar order.
- Every link to a page, an anchor or an image is checked. Links to pages that
  are not written yet only warn, unless --strict (use it before going live).
- `<!-- capture: what -->` marks a picture still to be taken; the list is
  printed at the end.
"""
import os
import re
import sys
import unicodedata

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import codeshot

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

SHOT_BEGIN = re.compile(r"^<!--\s*shot\b(.*?)-->\s*$")
SHOT_END = re.compile(r"^<!--\s*/shot\s*-->\s*$")
ATTRIBUTE = re.compile(r'(\w+)=(?:"([^"]*)"|(\S+))')
FENCE = re.compile(r"^(\s*)(```+|~~~+)\s*([\w#+-]*)")
CRUMB = re.compile(r"^<!--\s*crumb\s*-->.*?<!--\s*/crumb\s*-->\n*", re.S)
NAV = re.compile(r"\n*<!--\s*nav\s*-->.*?<!--\s*/nav\s*-->\n*", re.S)
LINK = re.compile(r"\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
HTML_LINK = re.compile(r"""\b(?:href|src)="([^"]+)\"""")
CODE_SPAN = re.compile(r"(`+).*?\1", re.S)
SIDEBAR_GROUP = re.compile(r"<summary>(?:<b>)?(.*?)(?:</b>)?</summary>|^\*\*([^*\[\]]+)\*\*\s*$")
SIDEBAR_LINK = re.compile(r"\[([^\]]+)\]\(([^)#\s/]+)\)")


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read().replace("\r\n", "\n")


def write(path, text):
    if os.path.isfile(path) and read(path) == text:
        return False
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return True


def pages_in(wiki):
    pages, errors = {}, []
    for directory, folders, files in os.walk(wiki):
        folders[:] = [d for d in folders if not d.startswith(".")]
        for name in files:
            if name.endswith(".md"):
                page = name[:-3]
                path = os.path.join(directory, name)
                if page in pages:
                    errors.append(f"two files make the page {page}: {pages[page]} and {path}")
                pages[page] = path
    return pages, errors


def slug(heading):
    text = re.sub(r"<[^>]+>", "", heading).strip().lower()
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    text = "".join(c for c in text if c in " -_" or unicodedata.category(c)[0] in "LN")
    return text.replace(" ", "-")


def anchors(text):
    found, counts, fence = set(), {}, False
    for line in text.split("\n"):
        if FENCE.match(line):
            fence = not fence
        elif not fence and re.match(r"^#{1,6}\s", line):
            base = slug(line.lstrip("#"))
            n = counts.get(base, 0)
            counts[base] = n + 1
            found.add(base if n == 0 else f"{base}-{n}")
    found.update(re.findall(r'<a (?:name|id)="([^"]+)"', text))
    return found


def attributes(text):
    return {m.group(1): m.group(2) if m.group(2) is not None else m.group(3) for m in ATTRIBUTE.finditer(text)}


def render_shots(page, text, wiki, errors):
    lines = text.split("\n")
    out, i, count = [], 0, 0
    while i < len(lines):
        begin = SHOT_BEGIN.match(lines[i])
        if not begin:
            out.append(lines[i])
            i += 1
            continue
        count += 1
        attrs = attributes(begin.group(1))
        j = i + 1
        while j < len(lines) and not FENCE.match(lines[j]):
            if SHOT_BEGIN.match(lines[j]) or SHOT_END.match(lines[j]):
                break
            j += 1
        if j >= len(lines) or not FENCE.match(lines[j]):
            errors.append(f"{page}: shot {count} has no code block")
            out.append(lines[i])
            i += 1
            continue
        indent, mark, lang = FENCE.match(lines[j]).groups()
        k = j + 1
        while k < len(lines) and not lines[k].strip().startswith(mark):
            k += 1
        if k >= len(lines):
            errors.append(f"{page}: shot {count} has an unclosed code block")
            out.extend(lines[i:])
            break
        code = "\n".join(lines[j + 1:k])
        end = k + 1
        scan = end
        while scan < len(lines) and (not lines[scan].strip() or lines[scan].strip() == "</details>"):
            scan += 1
        if scan < len(lines) and SHOT_END.match(lines[scan]):
            end = scan + 1
        name = attrs.get("name") or f"{page.lower()}-{count}"
        lang = attrs.get("lang") or lang or "text"
        title = attrs.get("title", "")
        image = f"images/code/{name}.png"
        width, _ = codeshot.render(code, os.path.join(wiki, image), title, lang,
                                   codeshot.parse_lines(attrs.get("hl")), attrs.get("numbers") != "no")
        alt = attrs.get("alt") or (f"Code: {title}" if title else "Code")
        out += [
            lines[i],
            f'<p align="center"><img src="{image}" alt="{alt}" width="{width}"></p>',
            "",
            "<details>",
            "<summary>Copy this code</summary>",
            "",
            f"{indent}{mark}{lang}",
            *lines[j + 1:k],
            f"{indent}{mark}",
            "",
            "</details>",
            "<!-- /shot -->",
        ]
        i = end
    return "\n".join(out)


def sidebar_order(wiki, pages):
    path = os.path.join(wiki, "_Sidebar.md")
    if not os.path.isfile(path):
        return []
    order, group = [], ""
    for line in read(path).split("\n"):
        heading = SIDEBAR_GROUP.search(line)
        if heading:
            group = re.sub(r"<[^>]+>", "", heading.group(1) or heading.group(2)).strip()
        for label, target in SIDEBAR_LINK.findall(line):
            if target in pages and target not in [p for p, _, _ in order]:
                order.append((target, re.sub(r"[*_`]", "", label), group))
    return order


def add_navigation(text, index, order):
    page, _, group = order[index]
    text = CRUMB.sub("", text)
    text = NAV.sub("\n", text).rstrip("\n")
    parts = []
    if index > 0 and order[index - 1][2] == group and group:
        prev, label, _ = order[index - 1]
        parts.append(f'<a href="{prev}">← {label}</a>')
    if index + 1 < len(order) and order[index + 1][2] == group and group:
        nxt, label, _ = order[index + 1]
        parts.append(f'<a href="{nxt}">{label} →</a>')
    crumb = f"<!-- crumb --><sub>{group}</sub><!-- /crumb -->\n\n" if group else ""
    nav = ("\n\n<!-- nav -->\n\n---\n\n<p align=\"center\">" + " &nbsp;·&nbsp; ".join(parts) +
           "</p>\n<!-- /nav -->") if parts else ""
    return crumb + text + nav + "\n"


def prose(text):
    out, fence = [], None
    for line in text.split("\n"):
        m = FENCE.match(line)
        if fence is None and m:
            fence = m.group(2)
            out.append("")
        elif fence is not None:
            if line.strip().startswith(fence):
                fence = None
            out.append("")
        else:
            out.append(line)
    return CODE_SPAN.sub("", "\n".join(out))


def check_links(page, text, wiki, pages, page_anchors, errors, pending):
    body = prose(text)
    targets = LINK.findall(body) + HTML_LINK.findall(body)
    for target in targets:
        if re.match(r"^[a-z][a-z0-9+.-]*:", target, re.I):
            continue
        path, _, anchor = target.partition("#")
        if not path:
            if anchor not in page_anchors[page]:
                errors.append(f"{page}: no heading for #{anchor}")
            continue
        if "/" in path or re.search(r"\.(png|jpe?g|gif|webp|svg|pdf|zip|txt|json|cs)$", path, re.I):
            if not os.path.isfile(os.path.join(wiki, path)):
                errors.append(f"{page}: missing file {path}")
            continue
        if path not in pages:
            pending.setdefault(path, set()).add(page)
        elif anchor and anchor not in page_anchors[path]:
            errors.append(f"{page}: no heading for {path}#{anchor}")


def main():
    args = sys.argv[1:]
    if any(a in ("-h", "--help") for a in args):
        sys.exit(__doc__)
    strict = "--strict" in args
    args = [a for a in args if a != "--strict"]
    wiki = os.path.abspath(args[0] if args else os.path.join(ROOT, "wiki"))
    if not os.path.isfile(os.path.join(wiki, "Home.md")):
        sys.exit(f"{wiki} has no Home.md")
    pages, errors = pages_in(wiki)
    pending = {}
    texts = {}
    for page, path in pages.items():
        texts[page] = render_shots(page, read(path), wiki, errors)
    order = sidebar_order(wiki, pages)
    for index, (page, _, _) in enumerate(order):
        texts[page] = add_navigation(texts[page], index, order)
    listed = {p for p, _, _ in order}
    if "_Sidebar" in texts:
        for _, target in SIDEBAR_LINK.findall(texts["_Sidebar"]):
            if target not in pages:
                pending.setdefault(target, set()).add("_Sidebar")
    for page in sorted(pages):
        if not page.startswith("_") and page not in listed:
            errors.append(f"{page} is not in _Sidebar.md")
    page_anchors = {page: anchors(text) for page, text in texts.items()}
    for page, text in texts.items():
        check_links(page, text, wiki, pages, page_anchors, errors, pending)
    changed = [page for page, text in texts.items() if write(pages[page], text)]
    print(f"{len(pages)} pages, {len(changed)} changed" + (f": {', '.join(sorted(changed))}" if changed else ""))
    captures = [(page, m.group(1).strip()) for page, text in sorted(texts.items())
                for m in re.finditer(r"<!--\s*capture:(.*?)-->", text, re.S)]
    if captures:
        print(f"{len(captures)} captures to take:")
        print("\n".join(f"  {page}: {what}" for page, what in captures))
    if pending:
        print(f"{len(pending)} pages not written yet: {', '.join(sorted(pending))}")
        if strict:
            errors += [f"{', '.join(sorted(sources))}: link to a missing page: {target}"
                       for target, sources in sorted(pending.items())]
    if errors:
        print("\n".join(errors), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
