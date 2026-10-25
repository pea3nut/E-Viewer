param([switch]$CompileOnly, [switch]$Smoke,
    [string]$BuildTools = 'C:\BuildTools')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $PSScriptRoot 'bin\Console'
New-Item -ItemType Directory -Force $output | Out-Null
$html = Join-Path $env:USERPROFILE '.nuget\packages\htmlagilitypack\1.11.70\lib\Net45\HtmlAgilityPack.dll'
if (!(Test-Path $html)) { throw 'Restore ExViewer.sln NuGet packages first (see repository CI).' }
$production = Join-Path $repo 'ExClient\Internal'
$sources = @('ThumbnailSource.cs','ThumbnailParser.cs','ThumbnailLoadState.cs','ThumbnailDownloadCache.cs') | ForEach-Object { Join-Path $production $_ }
$sources += (Join-Path $PSScriptRoot 'Regression.cs'), (Join-Path $PSScriptRoot 'ConsoleMain.cs')
& "$BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" /nologo /langversion:latest /warnaserror+ /target:exe "/out:$output\ThumbnailRegression.exe" "/reference:$html" $sources
if ($LASTEXITCODE -ne 0) { throw "Regression compile failed: $LASTEXITCODE" }
Copy-Item $html $output -Force
if (!$CompileOnly) {
    $arguments = @()
    if ($Smoke) { $arguments += '--smoke' }
    & "$output\ThumbnailRegression.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw "Regression failed: $LASTEXITCODE" }
}
