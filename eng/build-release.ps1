[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$BuildInstaller,

    [switch]$BuildEnterpriseMsi,

    [switch]$BuildBrowserExtension
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

    if ($BuildEnterpriseMsi) {
        $enterpriseProject = Join-Path $repositoryRoot 'packaging\enterprise\SnipArc.Msi.wixproj'
        dotnet build $enterpriseProject `
            --configuration $Configuration `
            "-p:PublishDir=$publishDirectory"
        $enterpriseOutput = Join-Path $repositoryRoot 'packaging\enterprise\bin\x64'
        $enterpriseMsi = Get-ChildItem -LiteralPath $enterpriseOutput -Recurse -Filter 'SnipArc-Enterprise-x64.msi' |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if (-not $enterpriseMsi) {
            throw 'The enterprise MSI build succeeded but its output could not be found.'
        }
        $enterpriseArtifactDirectory = Join-Path $artifactRoot 'enterprise'
        New-Item -ItemType Directory -Path $enterpriseArtifactDirectory -Force | Out-Null
        Copy-Item -LiteralPath $enterpriseMsi.FullName -Destination $enterpriseArtifactDirectory -Force
        Write-Host "Enterprise MSI output: $enterpriseArtifactDirectory"
    }

    if ($BuildBrowserExtension) {
        $extensionSource = Join-Path $repositoryRoot 'extensions\chromium'
        $extensionArtifactDirectory = Join-Path $artifactRoot 'extension'
        $extensionArtifact = Join-Path $extensionArtifactDirectory 'SnipArc-Browser-Capture-0.2.0.zip'
        if (-not (Test-Path -LiteralPath (Join-Path $extensionSource 'manifest.json'))) {
            throw "Browser extension manifest not found: $extensionSource"
        }

        New-Item -ItemType Directory -Path $extensionArtifactDirectory -Force | Out-Null
        if (Test-Path -LiteralPath $extensionArtifact) {
            Remove-Item -LiteralPath $extensionArtifact -Force
        }
        Compress-Archive -Path (Join-Path $extensionSource '*') `
            -DestinationPath $extensionArtifact `
            -CompressionLevel Optimal
        Write-Host "Browser extension output: $extensionArtifact"
    }

    if ($BuildInstaller) {
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
    else {
        Write-Host "Published application: $publishDirectory"
    }

    if ($BuildInstaller -or $BuildEnterpriseMsi -or $BuildBrowserExtension) {
        $releaseArtifacts = @(
            @{ RelativePath = 'installer/SnipArc-Setup-x64.exe'; FullPath = (Join-Path $artifactRoot 'installer\SnipArc-Setup-x64.exe') },
            @{ RelativePath = 'enterprise/SnipArc-Enterprise-x64.msi'; FullPath = (Join-Path $artifactRoot 'enterprise\SnipArc-Enterprise-x64.msi') },
            @{ RelativePath = 'extension/SnipArc-Browser-Capture-0.2.0.zip'; FullPath = (Join-Path $artifactRoot 'extension\SnipArc-Browser-Capture-0.2.0.zip') }
        )
        $checksumLines = $releaseArtifacts |
            Where-Object { Test-Path -LiteralPath $_.FullPath } |
            ForEach-Object {
                $hash = Get-FileHash -LiteralPath $_.FullPath -Algorithm SHA256
                "$($hash.Hash)  $($_.RelativePath)"
            }
        $checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
        Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding ascii
        Write-Host "Release checksums: $checksumPath"
    }
}
finally {
    Pop-Location
}
