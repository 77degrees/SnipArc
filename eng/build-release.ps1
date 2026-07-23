[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$BuildInstaller
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'ScreenCaptureApp.slnx'
$applicationProject = Join-Path $repositoryRoot 'src\ScreenCaptureApp.App\ScreenCaptureApp.App.csproj'
$iconGenerator = Join-Path $repositoryRoot 'eng\generate-icon.ps1'
$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactRoot 'app\win-x64'

Push-Location $repositoryRoot
try {
    & $iconGenerator
    dotnet restore $solutionPath
    dotnet build $solutionPath --configuration $Configuration --no-restore
    dotnet test $solutionPath --configuration $Configuration --no-build
    dotnet publish $applicationProject `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDirectory `
        --no-restore

    if (-not $BuildInstaller) {
        Write-Host "Published application: $publishDirectory"
        return
    }

    $installerScript = Join-Path $repositoryRoot 'installer\ScreenCaptureApp.iss'
    if (-not (Test-Path -LiteralPath $installerScript)) {
        throw "Installer script not found: $installerScript"
    }

    $innoCompiler = (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue).Source
    if (-not $innoCompiler) {
        $innoCompiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    }

    if (-not (Test-Path -LiteralPath $innoCompiler)) {
        throw 'Inno Setup compiler (ISCC.exe) was not found on PATH.'
    }

    & $innoCompiler "/DPublishDir=$publishDirectory" $installerScript
    Write-Host "Installer output: $(Join-Path $repositoryRoot 'artifacts\installer')"
}
finally {
    Pop-Location
}
