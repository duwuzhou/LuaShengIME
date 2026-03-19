param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,
    [Parameter(Mandatory = $true)]
    [string]$Configuration,
    [Parameter(Mandatory = $true)]
    [string]$KeyBase64,
    [switch]$IncludeDrawable,
    [switch]$IncludeValues
)

if ($Configuration -ne "Release") {
    exit 0
}

$resourcesDir = Join-Path $ProjectDir "Resources"
$assetsDir = Join-Path $ProjectDir "Assets\\enc"

$key = [Convert]::FromBase64String($KeyBase64)

if (Test-Path $assetsDir) {
    Remove-Item -Recurse -Force $assetsDir
}
New-Item -ItemType Directory -Path $assetsDir | Out-Null

$files = @()
$layoutDir = Join-Path $resourcesDir "layout"
if (Test-Path $layoutDir) {
    $files += Get-ChildItem -File -Recurse $layoutDir -Filter *.xml
}

if ($IncludeDrawable) {
    $drawableDir = Join-Path $resourcesDir "drawable"
    if (Test-Path $drawableDir) {
        $files += Get-ChildItem -File -Recurse $drawableDir -Filter *.xml
    }
}

if ($IncludeValues) {
    $valuesDir = Join-Path $resourcesDir "values"
    if (Test-Path $valuesDir) {
        $files += Get-ChildItem -File -Recurse $valuesDir -Filter *.xml
    }
}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($resourcesDir.Length).TrimStart('\', '/')
    $outPath = Join-Path $assetsDir ($relative + ".enc")
    $outDir = Split-Path -Parent $outPath
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir | Out-Null
    }

    $plain = [System.IO.File]::ReadAllBytes($file.FullName)
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key = $key
    $aes.GenerateIV()

    $ms = New-Object System.IO.MemoryStream
    $ms.Write($aes.IV, 0, $aes.IV.Length) | Out-Null
    $encryptor = $aes.CreateEncryptor()
    $cs = New-Object System.Security.Cryptography.CryptoStream($ms, $encryptor, [System.Security.Cryptography.CryptoStreamMode]::Write)
    $cs.Write($plain, 0, $plain.Length)
    $cs.FlushFinalBlock()
    $cs.Dispose()

    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $ms.Dispose()
    $aes.Dispose()
}
