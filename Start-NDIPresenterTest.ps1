[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$executable = Join-Path $projectDirectory 'release-v1.1.0\TractusPresenterTestForNDI.exe'

if (-not (Test-Path -LiteralPath $executable)) {
    Write-Host 'Building Tractus Presenter Test for NDI...'
    dotnet publish (Join-Path $projectDirectory 'TractusPresenterTestForNDI.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $projectDirectory 'release-v1.1.0')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

& $executable
