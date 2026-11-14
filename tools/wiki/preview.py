#!/usr/bin/env python3
"""Renders wiki pages with GitHub's own Markdown renderer into a local folder.

Usage: python tools/wiki/preview.py [--wiki DIR] [--out DIR] [page ...]

Open <out>/Home.html in a browser. Links to pages that do not exist yet show
dashed. Results are cached by content, so only changed pages hit the API
(60 calls an hour without a token).
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
API = "https://api.github.com/markdown"
CSS = "https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.8.1/github-markdown.min.css"
ALERT = re.compile(r"<blockquote>\s*<p>\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*(.*?)</blockquote>", re.S)
HREF = re.compile(r'href="([^"#:/]+)(#[^"]*)?"')

PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title} · Bunny Loader wiki preview</title>
<link rel="stylesheet" href="{css}">
<style>
body {{ margin: 0; background: #fff; }}
@media (prefers-color-scheme: dark) {{ body {{ background: #0d1117; }} }}
.wrap {{ max-width: 1280px; margin: 0 auto; padding: 24px 16px; display: grid; grid-template-columns: minmax(0, 1fr) 280px; gap: 24px; }}
@media (max-width: 900px) {{ .wrap {{ grid-template-columns: 1fr; }} }}
h1.title {{ font: 600 32px/1.25 -apple-system, "Segoe UI", sans-serif; margin: 0 0 16px; padding-bottom: 8px; border-bottom: 1px solid #d1d9e0; }}
aside .markdown-body {{ font-size: 14px; border: 1px solid #d1d9e0; border-radius: 6px; padding: 12px 16px; }}
.foot {{ grid-column: 1; border-top: 1px solid #d1d9e0; padding-top: 16px; }}
a.missing {{ color: #8b949e !important; text-decoration: underline dashed !important; }}
.markdown-alert {{ padding: 8px 16px; margin-bottom: 16px; border-left: 4px solid #0969da; }}
.markdown-alert-tip {{ border-color: #1a7f37; }} .markdown-alert-important {{ border-color: #8250df; }}
.markdown-alert-warning {{ border-color: #9a6700; }} .markdown-alert-caution {{ border-color: #cf222e; }}
.markdown-alert-title {{ font-weight: 600; margin-bottom: 4px; }}
</style></head>
<body><div class="wrap">
<main><h1 class="title">{title}</h1><article class="markdown-body">{body}</article></main>
<aside><div class="markdown-body">{sidebar}</div></aside>
<div class="foot markdown-body">{footer}</div>
</div></body></html>
"""


def render(markdown, cache):
    key = hashlib.sha256(markdown.encode("utf-8")).hexdigest()
    path = os.path.join(cache, key + ".html")
    if os.path.isfile(path):
        with open(path, encoding="utf-8") as f:
            return f.read()
    request = urllib.request.Request(API, data=json.dumps({"text": markdown, "mode": "markdown"}).encode("utf-8"),
                                     headers={"Accept": "application/vnd.github+json",
                                              "Content-Type": "application/json"})
    with urllib.request.urlopen(request) as response:
        html = response.read().decode("utf-8")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(html)
    return html


def polish(html, pages):
    def alert(m):
        kind = m.group(1).lower()
        return (f'<div class="markdown-alert markdown-alert-{kind}"><p class="markdown-alert-title">'
                f'{kind.capitalize()}</p><p>{m.group(2)}</div>')

    def link(m):
        name, anchor = m.group(1), m.group(2) or ""
        if "." in name:
            return m.group(0)
        if name in pages:
            return f'href="{name}.html{anchor}"'
        return f'class="missing" href="{name}.html{anchor}"'

    return HREF.sub(link, ALERT.sub(alert, html))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--wiki", default=os.path.join(ROOT, "wiki"))
    parser.add_argument("--out", default=os.path.join(ROOT, "build", "wiki-preview"))
    parser.add_argument("pages", nargs="*")
    args = parser.parse_args()
    pages = {}
    for directory, folders, files in os.walk(args.wiki):
        folders[:] = [d for d in folders if not d.startswith(".")]
        for name in files:
            if name.endswith(".md"):
                pages[name[:-3]] = os.path.join(directory, name)
    cache = os.path.join(args.out, ".cache")
    os.makedirs(cache, exist_ok=True)

    def source(page):
        with open(pages[page], encoding="utf-8") as f:
            return f.read()

    sidebar = polish(render(source("_Sidebar"), cache), pages) if "_Sidebar" in pages else ""
    footer = polish(render(source("_Footer"), cache), pages) if "_Footer" in pages else ""
    wanted = args.pages or [p for p in pages if not p.startswith("_")]
    for page in wanted:
        body = polish(render(source(page), cache), pages)
        title = page.replace("-", " ")
        with open(os.path.join(args.out, page + ".html"), "w", encoding="utf-8", newline="\n") as f:
            f.write(PAGE.format(title=title, css=CSS, body=body, sidebar=sidebar, footer=footer))
    images = os.path.join(args.wiki, "images")
    if os.path.isdir(images):
        shutil.copytree(images, os.path.join(args.out, "images"), dirs_exist_ok=True)
    print(f"{len(wanted)} pages in {args.out}")


if __name__ == "__main__":
    main()
