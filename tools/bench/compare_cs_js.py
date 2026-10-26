"""Junta as linhas "bench cs ..." e "bench js ..." (tools/tests/benchcs e
benchjs) numa tabela: caso, C#, JS e quantas vezes o JS e mais lento.

    adb logcat -d -s BunnyLoader | python tools/bench/compare_cs_js.py
    python tools/bench/compare_cs_js.py saida.txt
"""
import re
import sys

LINE = re.compile(r"bench (cs|js) (.+?): ([0-9.]+|n/a) ?(\S*)$")


def main() -> None:
    text = open(sys.argv[1], encoding="utf-8").read() if len(sys.argv) > 1 else sys.stdin.read()
    rows: dict[str, dict[str, tuple[str, str]]] = {}
    order: list[str] = []
    for raw in text.splitlines():
        m = LINE.search(raw.strip())
        if not m or m.group(2) in ("FIM", "sink"):
            continue
        lang, case, value, unit = m.groups()
        if case not in rows:
            rows[case] = {}
            order.append(case)
        rows[case][lang] = (value, unit)

    width = max(len(c) for c in order) if order else 10
    print(f"| {'case'.ljust(width)} | C# | QuickJS | JS / C# |")
    print(f"|{'-' * (width + 2)}|---:|---:|---:|")
    for case in order:
        cs = rows[case].get("cs", ("-", ""))
        js = rows[case].get("js", ("-", ""))
        ratio = "-"
        try:
            c, j = float(cs[0]), float(js[0])
            if c > 0:
                ratio = f"{j / c:.1f}x"
            elif j > 0:
                ratio = "inf"
        except ValueError:
            pass
        fmt = lambda v: f"{v[0]} {v[1]}".strip()
        print(f"| {case.ljust(width)} | {fmt(cs)} | {fmt(js)} | {ratio} |")


if __name__ == "__main__":
    main()
