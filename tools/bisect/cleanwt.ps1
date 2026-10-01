param([string]$W)
# Desfaz as juncoes da worktree e so entao apaga a pasta. Nunca segue juncao:
# um Remove-Item recursivo por cima de uma juncao apagaria os assets do jogo.
if (-not $W -or $W.Length -lt 20 -or -not (Test-Path -LiteralPath $W)) { "caminho invalido: '$W'"; exit 1 }
foreach ($rel in @('app\src\main\cpp\third_party\quickjs', 'terraria1456_assets\src\main\assets', 'licensing-classes')) {
    $j = Join-Path $W $rel
    $i = Get-Item -LiteralPath $j -Force -ErrorAction SilentlyContinue
    if ($i -and $i.LinkType -eq 'Junction') { cmd /c rmdir "$j" }
}
$left = Get-ChildItem -LiteralPath $W -Recurse -Force -Attributes ReparsePoint -ErrorAction SilentlyContinue
if ($left.Count -gt 0) { "juncao sobrou em $W : nao apaguei"; exit 1 }
# O prefixo \\?\ passa do limite de 260 caracteres (a build do CMake e funda).
Remove-Item -LiteralPath ('\\?\' + $W) -Recurse -Force -Confirm:$false
"worktree apagada: $(-not (Test-Path -LiteralPath $W))"
