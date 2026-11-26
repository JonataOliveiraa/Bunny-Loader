import re
import statistics
import sys

LINE = re.compile(r"case=(\S+) cold_us=([\d.]+) hot_ns=([\d.]+) min_ns=([\d.]+) alloc_b=([\d.]+) n=(\d+)")
ERROR = re.compile(r"case=(\S+) ERROR (.*)")


def main(paths):
    runs = {}
    errors = {}
    order = []
    for path in paths:
        for line in open(path, encoding="utf-8", errors="replace"):
            m = LINE.search(line)
            if m:
                name = m.group(1)
                if name not in runs:
                    runs[name] = []
                    order.append(name)
                runs[name].append(tuple(float(m.group(i)) for i in range(2, 6)))
                continue
            m = ERROR.search(line)
            if m:
                errors[m.group(1)] = m.group(2)

    def median(name, i):
        return statistics.median(r[i] for r in runs[name])

    base = {k: median(k, 1) for k in ("baseline/static_ToInt", "baseline/instance_CountBuffs") if k in runs}
    print(f"runs: {len(paths)}, cases: {len(order)}, errors: {len(errors)}")
    print(f"{'case':46} {'hot ns':>9} {'spread':>9} {'min ns':>8} {'cold us':>9} {'alloc B':>8}")
    for name in order:
        hots = [r[1] for r in runs[name]]
        spread = max(hots) - min(hots)
        print(f"{name:46} {median(name, 1):9.1f} {spread:9.1f} {median(name, 2):8.1f} {median(name, 0):9.1f} {median(name, 3):8.1f}")
    for name, text in errors.items():
        print(f"{name:46} ERROR {text}")
    if base:
        print()
        print("reference calls: " + ", ".join(f"{k.split('/')[1]} {v:.1f} ns" for k, v in base.items()))


if __name__ == "__main__":
    main(sys.argv[1:])
