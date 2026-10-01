param([string]$W, [string]$R)
# Liga na worktree $W o que o git nao guarda, a partir do repositorio $R:
# QuickJS, assets do jogo e licensing-classes por juncao; as .so por hard link.
# Desfeito pelo cleanwt.ps1 antes de apagar a worktree.
if (-not (Test-Path -LiteralPath $W) -or -not (Test-Path -LiteralPath $R)) { "caminho invalido"; exit 1 }
if ((Resolve-Path -LiteralPath $W).Path -eq (Resolve-Path -LiteralPath $R).Path) { "a worktree nao pode ser o repositorio"; exit 1 }
$links = @{
    'app\src\main\cpp\third_party\quickjs' = 'app\src\main\cpp\third_party\quickjs'
    'terraria1456_assets\src\main\assets'  = 'terraria1456_assets\src\main\assets'
    'licensing-classes'                    = 'licensing-classes'
}
foreach ($rel in $links.Keys) {
    $at = Join-Path $W $rel
    # A worktree traz a pasta vazia (so o README versionado): a juncao entra no lugar.
    if (Test-Path -LiteralPath $at) { Remove-Item -LiteralPath $at -Recurse -Force }
    New-Item -ItemType Junction -Path $at -Target (Join-Path $R $links[$rel]) | Out-Null
}
$libs = Join-Path $W 'app\src\main\jniLibs\arm64-v8a'
Get-ChildItem (Join-Path $R 'app\src\main\jniLibs\arm64-v8a\*.so') | ForEach-Object {
    New-Item -ItemType HardLink -Path (Join-Path $libs $_.Name) -Target $_.FullName | Out-Null
}
