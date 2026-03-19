param(
    [string]$Configuration = "Release",
    [bool]$CompatibilityMode = $true,
    [bool]$InstallToDevice = $true,
    [string]$DeviceSerial = "8f0dd01",
    [string]$ObfuscarExe = "C:\Users\Administrator\.nuget\packages\obfuscar\3.0.0-beta.4\tools\Obfuscar.Console.exe",
    [string]$KeyStorePath = "E:\Development Project\IME\qm\ime-release.keystore",
    [string]$KeyAlias = "ime-release",
    [string]$StorePass = "1VxY9eBindWK7CmqT2G5DcRg",
    [string]$KeyPass = "1VxY9eBindWK7CmqT2G5DcRg"
)

$ErrorActionPreference = "Stop"

function Assert-CommandExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Command not found: $Name"
    }
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Get-OnlineDeviceSerials {
    $lines = & adb devices
    if ($LASTEXITCODE -ne 0) {
        throw "adb devices failed with exit code $LASTEXITCODE"
    }

    $serials = @()
    foreach ($line in $lines) {
        if ($line -match "^([^\s]+)\s+device$") {
            $serials += $Matches[1]
        }
    }

    return $serials
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "..\IME.sln"))
$publishDir = Join-Path $projectRoot "bin\$Configuration\net8.0-android\publish"
$apkPath = Join-Path $publishDir "com.hualuo.luanshenIME-Signed.apk"

Assert-CommandExists -Name "dotnet"
Assert-PathExists -Path $solutionPath -Label "Solution"
Assert-PathExists -Path $ObfuscarExe -Label "Obfuscar"
Assert-PathExists -Path $KeyStorePath -Label "Keystore"

$publishArgs = @(
    "publish",
    $solutionPath,
    "-c", $Configuration,
    "/p:RunObfuscator=true",
    "/p:ObfuscarExe=$ObfuscarExe",
    "/p:AndroidKeyStore=true",
    "/p:AndroidSigningKeyStore=$KeyStorePath",
    "/p:AndroidSigningStorePass=$StorePass",
    "/p:AndroidSigningKeyAlias=$KeyAlias",
    "/p:AndroidSigningKeyPass=$KeyPass"
)

if ($CompatibilityMode) {
    $publishArgs += @(
        "/p:PublishTrimmed=false",
        "/p:TrimMode=false",
        "/p:RunAOTCompilation=false"
    )
}

Write-Host "Publishing $Configuration build..." -ForegroundColor Cyan
Write-Host "Solution: $solutionPath"
Write-Host "CompatibilityMode: $CompatibilityMode"
Write-Host "InstallToDevice: $InstallToDevice"

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Assert-PathExists -Path $apkPath -Label "Signed APK"
Write-Host "APK ready: $apkPath" -ForegroundColor Green

if (-not $InstallToDevice) {
    exit 0
}

Assert-CommandExists -Name "adb"

$onlineDevices = @(Get-OnlineDeviceSerials)
if ($onlineDevices.Count -eq 0) {
    throw "No adb device detected. APK is ready: $apkPath"
}

$resolvedDeviceSerial = $DeviceSerial
if ([string]::IsNullOrWhiteSpace($resolvedDeviceSerial)) {
    if ($onlineDevices.Count -ne 1) {
        throw "Multiple adb devices detected. Use -DeviceSerial. Online devices: $($onlineDevices -join ', ')"
    }

    $resolvedDeviceSerial = $onlineDevices[0]
}
elseif ($resolvedDeviceSerial -notin $onlineDevices) {
    if ($onlineDevices.Count -eq 1) {
        Write-Warning "Configured device '$resolvedDeviceSerial' is offline. Using '$($onlineDevices[0])' instead."
        $resolvedDeviceSerial = $onlineDevices[0]
    }
    else {
        throw "Configured device '$resolvedDeviceSerial' not found. Online devices: $($onlineDevices -join ', ')"
    }
}

$adbArgs = @("-s", $resolvedDeviceSerial, "install", "-r")
$adbArgs += $apkPath

Write-Host "Installing APK..." -ForegroundColor Cyan
Write-Host "Device: $resolvedDeviceSerial"
& adb @adbArgs
if ($LASTEXITCODE -ne 0) {
    throw "adb install failed with exit code $LASTEXITCODE"
}

Write-Host "Install finished." -ForegroundColor Green
