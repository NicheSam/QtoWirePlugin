param(
    [switch]$SkipBuild,
    [switch]$SkipUi
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'QtoWirePlugin.csproj'
$manifest = Join-Path $root 'QtoWirePlugin_v0.8.bundle\PackageContents.xml'
$releaseDirectory = Join-Path $root 'bin\x64\Release'
$pluginDll = Join-Path $releaseDirectory 'QtoWirePlugin.dll'
$fixture = Join-Path $root 'tests\fixtures\sample.qto_catalog.json'
$smokeSource = Join-Path $PSScriptRoot 'QtoCatalogSmokeTest.cs'
$smokeExe = Join-Path $releaseDirectory 'QtoCatalogSmokeTest.exe'
$excelSmokeSource = Join-Path $PSScriptRoot 'QtoExcelSmokeTest.cs'
$excelSmokeExe = Join-Path $releaseDirectory 'QtoExcelSmokeTest.exe'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'
$csc = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'

if (-not $SkipBuild) {
    & $msbuild $project /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}

$projectText = Get-Content -Raw -Encoding utf8 $project
$compiledSources = [regex]::Matches($projectText, '<Compile Include="([^"]+\.cs)"') | ForEach-Object { Join-Path $root $_.Groups[1].Value }
$sourceText = ($compiledSources | ForEach-Object { Get-Content -LiteralPath $_ -Encoding utf8 }) -join "`n"
$sourceCommands = [regex]::Matches($sourceText, 'CommandMethod\("([^"]+)"\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$manifestText = Get-Content -Raw -Encoding utf8 $manifest
$manifestCommands = [regex]::Matches($manifestText, '<Command Global="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$commandDiff = Compare-Object @($sourceCommands) @($manifestCommands)
if ($commandDiff) {
    $commandDiff | Format-Table | Out-String | Write-Host
    throw 'CommandMethod and bundle manifest commands do not match.'
}

$assemblyInfo = Get-Content -Raw -Encoding utf8 (Join-Path $root 'Properties\AssemblyInfo.cs')
if ($assemblyInfo -notmatch '0\.8\.2\.0' -or $manifestText -notmatch 'AppVersion="0\.8\.2"') {
    throw 'Assembly or manifest version is not v0.8.2.'
}

$dictionaryRoot = Join-Path $root 'QtoWirePlugin_v0.8.bundle\m2_m4_shared_dictionary'
foreach ($required in @('system_codes.csv', 'equipment_types.csv', 'cad_block_mapping.csv', 'bid_item_mapping.csv', 'review_rules.csv')) {
    if (-not (Test-Path (Join-Path $dictionaryRoot $required))) {
        throw "Bundle dictionary is missing $required."
    }
}

$bundleDll = Join-Path $root 'QtoWirePlugin_v0.8.bundle\Contents\Windows\QtoWirePlugin.dll'
$installerRoot = Join-Path $root 'release\QtoWirePlugin_v0.8.2_installer'
$installerDll = Join-Path $installerRoot 'QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll'
$installerManifest = Join-Path $installerRoot 'QtoWirePlugin.bundle\PackageContents.xml'
$installerZip = Join-Path $root 'release\QtoWirePlugin_v0.8.2_installer.zip'
foreach ($requiredPath in @($bundleDll, $installerDll, $installerManifest, $installerZip)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Release artifact is missing: $requiredPath" }
}

$bundleSystemCodeFile = Join-Path $dictionaryRoot 'system_codes.csv'
$installerSystemCodeFile = Join-Path $installerRoot 'QtoWirePlugin.bundle\m2_m4_shared_dictionary\system_codes.csv'
$bundleSystemCodes = @(Import-Csv -LiteralPath $bundleSystemCodeFile -Encoding UTF8)
$installerSystemCodes = @(Import-Csv -LiteralPath $installerSystemCodeFile -Encoding UTF8)
if ($bundleSystemCodes.Count -ne 8 -or $installerSystemCodes.Count -ne 8) {
    throw 'System code defaults must contain exactly eight rows.'
}
if ((Get-FileHash -LiteralPath $bundleSystemCodeFile -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $installerSystemCodeFile -Algorithm SHA256).Hash) {
    throw 'Source bundle and installer system code defaults do not match.'
}

$hashes = Get-FileHash @($pluginDll, $bundleDll, $installerDll) -Algorithm SHA256
if (($hashes.Hash | Sort-Object -Unique).Count -ne 1) {
    $hashes | Format-Table | Out-String | Write-Host
    throw 'Build, bundle, and installer DLL hashes do not match.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::OpenRead($installerZip)
try {
    $zipDllEntry = $zipArchive.Entries | Where-Object {
        $_.FullName -eq 'QtoWirePlugin.bundle/Contents/Windows/QtoWirePlugin.dll' -or
        $_.FullName -eq 'QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll'
    } | Select-Object -First 1
    if ($null -eq $zipDllEntry) { throw 'Installer ZIP does not contain QtoWirePlugin.dll at the expected path.' }

    $zipDllStream = $zipDllEntry.Open()
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $zipDllHash = ([BitConverter]::ToString($sha256.ComputeHash($zipDllStream))).Replace('-', '')
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $zipDllStream.Dispose()
    }
}
finally {
    $zipArchive.Dispose()
}

if ($zipDllHash -ne $hashes[0].Hash) {
    throw 'Installer ZIP contains a stale QtoWirePlugin.dll.'
}

$installerManifestText = Get-Content -Raw -Encoding utf8 $installerManifest
$installerCommands = [regex]::Matches($installerManifestText, '<Command Global="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
if (Compare-Object @($manifestCommands) @($installerCommands)) {
    throw 'Source bundle and installer command manifests do not match.'
}
if ($installerManifestText -notmatch 'AppVersion="0\.8\.2"') {
    throw 'Installer manifest version is not v0.8.2.'
}

& $csc /nologo /target:exe /out:$smokeExe /reference:$pluginDll /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $smokeSource
if ($LASTEXITCODE -ne 0) { throw 'Smoke test compilation failed.' }

$arguments = @($fixture)
if ($SkipUi) { $arguments += '--no-ui' }
& $smokeExe @arguments
if ($LASTEXITCODE -ne 0) { throw 'Catalog smoke test failed.' }

$windowsBase = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\WindowsBase.dll'
& $csc /nologo /target:exe /out:$excelSmokeExe /reference:$pluginDll /reference:System.dll /reference:System.Core.dll /reference:$windowsBase $excelSmokeSource
if ($LASTEXITCODE -ne 0) { throw 'Excel smoke test compilation failed.' }
& $excelSmokeExe
if ($LASTEXITCODE -ne 0) { throw 'Excel smoke test failed.' }

Write-Host "Validation passed. Commands=$($sourceCommands.Count); Version=0.8.2"
