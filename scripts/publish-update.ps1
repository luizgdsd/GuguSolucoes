param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$Mandatory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    $status = git status --short
    if ($status) {
        throw "Ha alteracoes nao commitadas. Faça commit antes de publicar um update."
    }

    $tag = "v$Version"
    git rev-parse $tag *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "A tag $tag ja existe."
    }

    git tag -a $tag -m "GuguSolucoes $Version"
    git push origin main
    git push origin $tag

    if ($Mandatory) {
        Write-Host "Tag enviada. Para marcar como obrigatorio, rode o workflow Release manualmente com mandatory=true." -ForegroundColor Yellow
    } else {
        Write-Host "Tag enviada. O GitHub Actions vai criar a release automaticamente: $tag" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
