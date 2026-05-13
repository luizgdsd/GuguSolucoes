param(
    [string]$AppVersion = "1.0.0",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SelfContained = $false,
    [switch]$SingleFile = $false,
    [string]$OutputDir = (Join-Path $PSScriptRoot "dist")
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "src\GuguSolucoes.Desktop\GuguSolucoes.Desktop.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Projeto nao encontrado: $projectPath"
}

$publishDir = Join-Path $OutputDir "publish"
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "[INFO] Publicando app .NET..." -ForegroundColor Gray
$publishArgs = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "/p:PublishSingleFile=$($SingleFile.IsPresent.ToString().ToLowerInvariant())",
    "/p:PublishTrimmed=false",
    "/p:Version=$AppVersion",
    "/p:FileVersion=$AppVersion.0",
    "/p:InformationalVersion=$AppVersion"
)

if ($SingleFile) {
    $publishArgs += "/p:IncludeNativeLibrariesForSelfExtract=true"
}

if ($SelfContained) {
    $publishArgs += "/p:SelfContained=true"
}
else {
    $publishArgs += "/p:SelfContained=false"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "Falha no dotnet publish."
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $isccPath) {
    throw @"
ISCC.exe nao encontrado.
Instale o Inno Setup 6 e execute novamente:
  winget install --id JRSoftware.InnoSetup -e
"@
}

$issPath = Join-Path $PSScriptRoot "installer\GuguSolucoes.iss"
if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Arquivo .iss nao encontrado: $issPath"
}

Write-Host "[INFO] Compilando instalador..." -ForegroundColor Gray
$issArgs = @(
    "/Qp",
    "/DMyAppVersion=$AppVersion",
    "/DMyPublishDir=$publishDir",
    "/DMyOutputDir=$OutputDir",
    $issPath
)

& $isccPath @issArgs
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o instalador Inno Setup."
}

$setupFile = Join-Path $OutputDir "GuguSolucoes-Setup-$AppVersion.exe"
if (-not (Test-Path -LiteralPath $setupFile)) {
    Write-Host "[WARN] Build concluido, mas setup esperado nao encontrado. Verifique a pasta dist." -ForegroundColor Yellow
}
else {
    Write-Host "[OK] Setup gerado: $setupFile" -ForegroundColor Green
}
