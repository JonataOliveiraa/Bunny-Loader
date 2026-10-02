"""Atualiza o script do Editor e monta o ZIP do Templo Pirâmide."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[2]
MOD = ROOT / "samples" / "TemploPiramide"
OUT = ROOT / "out"


def main():
    plan = (MOD / "content" / "temple-plan.js").read_text(encoding="utf-8")
    runtime = (MOD / "content" / "temple-runtime.js").read_text(encoding="utf-8")
    runtime = runtime.replace("import { Cell, createTemplePlan } from './temple-plan.js';\n", "")
    runtime = runtime.replace("export function buildTemple", "function buildTemple")
    plan = plan.replace("export const Cell", "const Cell").replace("export function createTemplePlan", "function createTemplePlan")
    header = """// Templo Pirâmide: execute no Editor e CRIE UM MUNDO NOVO sem fechar o jogo.
// Gerado por tools/temple/package.py a partir de samples/TemploPiramide/content/.
// Execute apenas o script OU o pacote do mod. Reexecutar atualiza o desenho.
// Compatível com o hook do script anterior: buildTemple e templeHooked.
(() => {
"""
    footer = """
globalThis.buildTemple = buildTemple;
if (!globalThis.templeHooked) {
    Terraria.WorldGen['void makeTemple(int x, int y, GenerationProgress progress)'].hook((_original, x, y) => {
        globalThis.buildTemple(x, y);
    });
    globalThis.templeHooked = true;
}
})();
'Templo Pirâmide pronto: crie um mundo novo';
"""
    editor = ROOT / "tools" / "editor" / "templo.js"
    editor.write_text(header + plan + "\n" + runtime + footer, encoding="utf-8")
    OUT.mkdir(exist_ok=True)
    archive = OUT / "TemploPiramide.zip"
    with ZipFile(archive, "w", ZIP_DEFLATED) as z:
        for file in sorted(MOD.rglob("*")):
            if file.is_file():
                z.write(file, file.relative_to(MOD).as_posix())
    print(f"Editor: {editor}")
    print(f"Pacote: {archive}")


if __name__ == "__main__":
    main()
