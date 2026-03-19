param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,
    [Parameter(Mandatory = $true)]
    [string]$Configuration,
    [string]$ObfuscarExe = "obfuscar",
    [switch]$ReplaceOutput
)

if ($Configuration -ne "Release") {
    exit 0
}

$inPath = Join-Path $ProjectDir "bin\\$Configuration\\net8.0-android"
$outPath = Join-Path $inPath "obfuscated"
$configPath = Join-Path $ProjectDir "obj\\obfuscar.generated.xml"

$envObfuscar = $env:OBFUSCAR_EXE
if (-not [string]::IsNullOrWhiteSpace($envObfuscar) -and $ObfuscarExe -eq "obfuscar") {
    $ObfuscarExe = $envObfuscar
}

$cmd = Get-Command $ObfuscarExe -ErrorAction SilentlyContinue
if (-not $cmd) {
    throw "Obfuscar executable not found. Set -ObfuscarExe or OBFUSCAR_EXE to Obfuscar.Console.exe."
}

$configText = @"
<?xml version="1.0"?>
<Obfuscator>
  <Var name="OutPath" value="$outPath" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="KeepPublicApi" value="false" />
  <Var name="RenameProperties" value="true" />
  <Var name="RenameEvents" value="true" />
  <Var name="UseUnicodeNames" value="false" />
  <Var name="StringEncryption" value="true" />

  <Module file="$inPath\\IME.dll">
    <SkipType name="IME.Features.Settings.MainActivity" />
    <SkipType name="IME.Features.Settings.SettingsActivity" />
    <SkipType name="IME.Features.Settings.KamiVipActivity" />
    <SkipType name="IME.Features.Prediction.PredictionManagerActivity" />
    <SkipType name="IME.Features.Shortcuts.ShortcutManagerActivity" />
    <SkipType name="IME.Features.Keyboard.QuickSymbolEditorActivity" />
    <SkipType name="IME.Features.Input.Ime" />
  </Module>
</Obfuscator>
"@

Set-Content -Encoding UTF8 -Path $configPath -Value $configText
& $cmd.Path $configPath
if ($LASTEXITCODE -ne 0) {
    throw "Obfuscar failed with exit code $LASTEXITCODE"
}

if ($ReplaceOutput) {
    $inPath = Join-Path $ProjectDir "bin\\$Configuration\\net8.0-android"
    $outPath = Join-Path $inPath "obfuscated"
    $dll = Join-Path $outPath "IME.dll"
    if (-not (Test-Path $dll)) {
        throw "Obfuscated IME.dll not found at $dll"
    }
    Copy-Item -Force $dll (Join-Path $inPath "IME.dll")
}
