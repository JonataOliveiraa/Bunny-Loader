# Testes do carregador de texturas

O teste nativo compila `TexturePacks.cpp` de producao contra simulacoes de IL2CPP/Unity:

```bash
python3 tools/tests/texturepacks/run.py
```

Requer Linux/WSL e g++. Confere as tabelas, prioridade, fallback de arquivo invalido, caminhos completos e extensao maiuscula, isolamento de symlinks, restaura valor/fonte/estado e confere que os handles do GC sao liberados. Tambem simula uma carga futura, repeticao do pedido e uma chamada fora da thread do jogo.

`device/` testa a primeira etapa no jogo. `named-device/` testa os assets individuais, outras tabelas e subpastas. Essas fixtures sao mods de teste; os packs de textura usados por elas nao precisam de scripts.

Para `named-device/`, forneca `content/expectations.json`:

```json
{
  "name": "meu-teste",
  "rows": [
    {"path": "Images/Inventory_Back", "field": "InventoryBack", "width": 52, "height": 52},
    {"path": "Images/Background_325", "field": "Background", "index": 325, "width": 1024, "height": 546},
    {"path": "Images/UI/Minimap/Default/MinimapFrame", "width": 256, "height": 264}
  ]
}
```

Uma linha sem `field` cria um Asset<Texture2D> vazio, chama explicitamente `.ctor(string name)` e verifica a carga futura. `Asset.new()` nao inicializa o nome. O teste espera 120 atualizacoes do jogo antes de conferir os assets. `vanilla: true` confere que as substituicoes foram desativadas; `world: true` pede a abertura do inventario na copia do mundo.

Os resultados dos ports locais estao em `ports/textures/VALIDATION.md`.
