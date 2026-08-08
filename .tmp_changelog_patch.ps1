$path = "D:\Dev Drive\BOCCHI\CHANGELOG.md"
$c = Get-Content -LiteralPath $path -Raw
if ($c -like "*recognizes South Horn knowledge crystals*") {
    Write-Output "already updated"
    exit 0
}
$needle = "### Fixes`r`n- Clicking **Path**"
$insert = "### Fixes`r`n- **Apply Buffs** recognizes South Horn knowledge crystals again when you stand at them (including right next to the crystal, not only on the thin buff ring).`r`n- Clicking **Path**"
if (-not $c.Contains("### Fixes`r`n- Clicking **Path**")) {
    $needle = "### Fixes`n- Clicking **Path**"
    $insert = "### Fixes`n- **Apply Buffs** recognizes South Horn knowledge crystals again when you stand at them (including right next to the crystal, not only on the thin buff ring).`n- Clicking **Path**"
}
if (-not $c.Contains($needle)) {
    Write-Error "needle not found"
    exit 1
}
$c2 = $c.Replace($needle, $insert)
Set-Content -LiteralPath $path -Value $c2 -NoNewline
Write-Output "updated"
