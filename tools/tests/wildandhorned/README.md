# Testes de Wild and Horned

A suíte usa o código do pacote Wild and Horned. Quando ele não estiver em `samples/WildAndHorned`, extraia a versão desejada do `.bl` em uma pasta temporária e informe a raiz que contém `content/`:

```powershell
$env:BL_WILDANDHORNED_ROOT = 'C:/Scripts/Bunny Loader/build/wildandhorned/github-validation'
node --experimental-vm-modules tools/tests/wildandhorned/check.mjs
```

O pacote publicado está nas [releases de Wild and Horned](https://github.com/JonataOliveiraa/Bunny-Loader/releases/tag/mod-wildandhorned-v1.5.2). A suíte verifica comportamento em um ambiente JavaScript controlado. `content/main.js` é a fixture separada para o jogo, depende do mod instalado e registra resultados `wildandhorned-qa`; execução automatizada não substitui validação no dispositivo.
