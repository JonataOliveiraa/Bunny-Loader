# tools/ — ferramentas de PC

| Script | O que faz |
|---|---|
| [`dump.sh`](dump.sh) | Regenera `refs/` a partir do jogo instalado (pull + extração + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extrai o bloco de uma classe/struct/enum do `refs/dump.cs`. |

```bash
tools/dump.sh                                   # pipeline completo
tools/dump.sh --skip-pull                       # reaproveita refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspecionar uma classe
```

## Futuro

- **bmod-packer** — empacota uma pasta de mod em `.bmod` (zip com `mod.json` + `main.js`).
- **lrc / lrbridgegen** — só entram no track da LRVM (mods em C#), Fase 6+.
