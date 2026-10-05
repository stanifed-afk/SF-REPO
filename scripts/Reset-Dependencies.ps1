[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$ClearGlobalCache
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "SignatureManager.sln"
$logDirectory = Join-Path $repositoryRoot "artifacts\dependency-reset"

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installationPath = & $vswhere -latest -products * -version "[16.0,17.0)" -requires Microsoft.Component.MSBuild -property installationPath
        if ($LASTEXITCODE -eq 0 -and $installationPath) {
            $candidate = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }

    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw "Nie znaleziono MSBuild. Zainstaluj w Visual Studio 2019 obciążenie 'Programowanie aplikacji klasycznych dla platformy .NET' oraz .NET Framework 4.8 Targeting Pack."
}

function Invoke-MSBuildStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $logPath = Join-Path $logDirectory ($Name + ".log")
    Write-Host "Uruchamianie: MSBuild $($Arguments -join ' ')"
    & $script:msbuildPath @Arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "Etap '$Name' zakończył się błędem. Pierwszy właściwy błąd znajduje się w pliku: $logPath"
    }
}

if (-not (Test-Path $solutionPath)) { throw "Nie znaleziono rozwiązania: $solutionPath" }
Write-Host "Zamknij Visual Studio przed resetem, aby żaden plik w katalogach .vs/bin/obj nie był zablokowany."

@(
    (Join-Path $repositoryRoot ".vs"),
    (Join-Path $repositoryRoot "SignatureManager\bin"),
    (Join-Path $repositoryRoot "SignatureManager\obj")
) | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "Usuwanie: $_"
        Remove-Item -Path $_ -Recurse -Force
    }
}

if ($ClearGlobalCache) {
    $globalPackages = Join-Path $env:USERPROFILE ".nuget\packages"
    if (Test-Path $globalPackages) {
        Write-Warning "Usuwanie globalnej pamięci NuGet używanej także przez inne projekty: $globalPackages"
        Remove-Item -Path $globalPackages -Recurse -Force
    }
}

New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
$script:msbuildPath = Find-MSBuild
Write-Host "MSBuild: $script:msbuildPath"

Invoke-MSBuildStep -Name "restore" -Arguments @(
    $solutionPath, "/t:Restore", "/m", "/v:minimal",
    "/p:RestoreForce=true", "/p:RestoreIgnoreFailedSources=false"
)
Invoke-MSBuildStep -Name "rebuild" -Arguments @(
    $solutionPath, "/t:Rebuild", "/m", "/v:minimal", "/p:Configuration=$Configuration"
)

Write-Host "Zależności odtworzono, a rozwiązanie przebudowano poprawnie." -ForegroundColor Green
