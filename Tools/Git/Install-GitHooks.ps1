[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

Push-Location -LiteralPath $repositoryRoot
try {
    $gitRoot = & git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0) {
        throw 'This script must be inside a Git checkout.'
    }
    if ([System.IO.Path]::GetFullPath($gitRoot) -ne $repositoryRoot) {
        throw 'The script directory does not match the Git repository root.'
    }

    $currentHooksPath = & git config --get core.hooksPath
    $configExitCode = $LASTEXITCODE
    if ($configExitCode -notin @(0, 1)) {
        throw 'Could not read the current Git hooks configuration.'
    }
    if ($currentHooksPath -and $currentHooksPath -ne '.githooks') {
        throw "An existing hooks path is configured: $currentHooksPath. Integrate the font check there instead."
    }

    if (-not $currentHooksPath) {
        $defaultHooksPath = & git rev-parse --git-path hooks
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not locate the existing Git hooks directory.'
        }
        if (Test-Path -LiteralPath $defaultHooksPath) {
            $existingHooks = @(Get-ChildItem -LiteralPath $defaultHooksPath -File | Where-Object { $_.Name -notlike '*.sample' })
            if ($existingHooks.Count -gt 0) {
                throw 'Existing Git hooks would be disabled. Integrate the font check into those hooks instead.'
            }
        }
    }

    & git config --local core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not install the repository Git hooks.'
    }
    & git config --local merge.renormalize true
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not enable merge renormalization.'
    }

    Write-Host 'Installed font asset pre-commit checks for this checkout.'
    Write-Host 'Enabled merge.renormalize for line-ending compatibility.'
    Write-Host 'Other collaborators must run this installer once in their own checkout.'
}
finally {
    Pop-Location
}
