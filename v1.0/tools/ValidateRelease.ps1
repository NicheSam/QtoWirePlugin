param(
    [switch]$SkipBuild,
    [switch]$SkipUi,
    [string]$BudgetSamplePath = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'QtoWirePlugin.csproj'
$manifest = Join-Path $root 'QtoWirePlugin_v1.0.bundle\PackageContents.xml'
$releaseDirectory = Join-Path $root 'bin\x64\Release'
$pluginDll = Join-Path $releaseDirectory 'QtoWirePlugin.dll'
$fixture = Join-Path $root 'tests\fixtures\sample.qto_catalog.json'
$smokeSource = Join-Path $PSScriptRoot 'QtoCatalogSmokeTest.cs'
$smokeExe = Join-Path $releaseDirectory 'QtoCatalogSmokeTest.exe'
$excelSmokeSource = Join-Path $PSScriptRoot 'QtoExcelSmokeTest.cs'
$excelSmokeExe = Join-Path $releaseDirectory 'QtoExcelSmokeTest.exe'
$budgetSmokeSource = Join-Path $PSScriptRoot 'QtoBudgetMappingSmokeTest.cs'
$budgetSmokeExe = Join-Path $releaseDirectory 'QtoBudgetMappingSmokeTest.exe'
$acdb = 'C:\Program Files\Autodesk\AutoCAD 2023\AcDbMgd.dll'
$fixtureDwg = Join-Path $root 'tests\fixtures\qto_validation_fixture.dwg'
$fixtureReport = Join-Path $root 'tests\fixtures\qto_validation_report.txt'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'
$csc = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'

if (-not $SkipBuild) {
    & $msbuild $project /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}

$projectText = Get-Content -Raw -Encoding utf8 $project
$compiledSources = [regex]::Matches($projectText, '<Compile Include="([^"]+\.cs)"') | ForEach-Object { Join-Path $root $_.Groups[1].Value }
$sourceText = ($compiledSources | ForEach-Object { Get-Content -LiteralPath $_ -Encoding utf8 }) -join "`n"
$ribbonSource = Get-Content -Raw -Encoding utf8 (Join-Path $root 'QtoRibbon.cs')
$scopeSource = Get-Content -Raw -Encoding utf8 (Join-Path $root 'QtoScopeCommands.cs')
if ($ribbonSource -match 'QtoSyncCommandService\.StartSync\s*\(') {
    throw 'AutoCAD startup must not start Excel synchronization.'
}
if ($scopeSource -match 'QtoSyncCommandService\.StartSync\s*\(') {
    throw 'Scope commands must not start Excel synchronization.'
}
if ($sourceText -match 'MarkHandledButtonClick|IgnoreButtonClick|HandledReviewIds|IgnoredReviewIds') {
    throw 'Review instructions reference removed fake actions.'
}
if ($sourceText -match 'new\s+QtoSettingsForm\s*\(') {
    throw 'The removed non-persistent settings form must not be reachable.'
}
$syncPaletteSource = Get-Content -Raw -Encoding utf8 (Join-Path $root 'QtoSyncMainPalette.cs')
if ($syncPaletteSource -notmatch 'ScrollControlIntoView\s*\(\s*logListBox\s*\)') {
    throw 'The sync log button must navigate to the log instead of showing a placeholder message.'
}
if ($syncPaletteSource -match 'RepairButtonClick' -or $syncPaletteSource -notmatch 'ValidateButtonClick') {
    throw 'The sync controller must expose one combined check-and-repair workflow.'
}
if ($sourceText -match 'Application\.ShowModelessDialog\s*\(\s*new\s+QtoReviewForm') {
    throw 'Review windows must use the shared external-window host.'
}
$sourceCommands = [regex]::Matches($sourceText, 'CommandMethod\("([^"]+)"\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$sourceCommands = @($sourceCommands | Where-Object { $_ -notin @('QTO_CREATE_VALIDATION_FIXTURE', 'QTO_VALIDATE_VALIDATION_FIXTURE') })
$manifestText = Get-Content -Raw -Encoding utf8 $manifest
$manifestCommands = [regex]::Matches($manifestText, '<Command Global="([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$commandDiff = Compare-Object @($sourceCommands) @($manifestCommands)
if ($commandDiff) {
    $commandDiff | Format-Table | Out-String | Write-Host
    throw 'CommandMethod and bundle manifest commands do not match.'
}

$assemblyInfo = Get-Content -Raw -Encoding utf8 (Join-Path $root 'Properties\AssemblyInfo.cs')
if ($assemblyInfo -notmatch '1\.0\.0\.0' -or $manifestText -notmatch 'AppVersion="1\.0\.0"') {
    throw 'Assembly or manifest version is not v1.0.0.'
}

$dictionaryRoot = Join-Path $root 'QtoWirePlugin_v1.0.bundle\m2_m4_shared_dictionary'
foreach ($required in @('system_codes.csv', 'equipment_types.csv', 'cad_block_mapping.csv', 'bid_item_mapping.csv', 'review_rules.csv')) {
    if (-not (Test-Path (Join-Path $dictionaryRoot $required))) {
        throw "Bundle dictionary is missing $required."
    }
}

$bundleDll = Join-Path $root 'QtoWirePlugin_v1.0.bundle\Contents\Windows\QtoWirePlugin.dll'
$installerRoot = Join-Path $root 'release\QtoWirePlugin_v1.0.0-beta_installer'
$installerDll = Join-Path $installerRoot 'QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll'
$installerManifest = Join-Path $installerRoot 'QtoWirePlugin.bundle\PackageContents.xml'
$installerValidationGuide = Join-Path $installerRoot 'docs\QTO_V1_0_BETA_VALIDATION.md'
$installerSecondPcChecklist = Join-Path $installerRoot 'docs\QTO_V1_0_SECOND_PC_CHECKLIST.md'
$installerBat = Join-Path $installerRoot 'install_or_update_QtoWirePlugin.bat'
$installerCheckBat = Join-Path $installerRoot 'check_installation.bat'
$installerSelfTestBat = Join-Path $installerRoot 'run_plugin_self_test.bat'
$installerSelfTestScript = Join-Path $installerRoot 'run_installed_self_test.ps1'
$installerZip = Join-Path $root 'release\QtoWirePlugin_v1.0.0-beta_installer.zip'
foreach ($requiredPath in @($bundleDll, $installerDll, $installerManifest, $installerValidationGuide, $installerSecondPcChecklist, $installerBat, $installerCheckBat, $installerSelfTestBat, $installerSelfTestScript, $installerZip)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Release artifact is missing: $requiredPath" }
}
if (-not (Test-Path -LiteralPath $fixtureDwg) -or -not (Test-Path -LiteralPath $fixtureReport)) {
    throw 'Synthetic CAD validation fixture or report is missing.'
}
$fixtureReportText = Get-Content -Raw -Encoding utf8 $fixtureReport
if ($fixtureReportText -notmatch 'Status=PASS') {
    throw 'Synthetic CAD validation fixture did not pass.'
}
if ($fixtureReportText -notmatch 'SelectedScopePreserved=True') {
    throw 'Synthetic CAD validation did not prove that selected scope apply preserves other scopes.'
}
if ($fixtureReportText -notmatch 'ScopeKindsIndependent=True') {
    throw 'Synthetic CAD validation did not prove that floor and system scopes are independent.'
}
if ($fixtureReportText -notmatch 'BlockUpdatePreserved=True') {
    throw 'Synthetic CAD validation did not prove that block updates preserve QTO instances.'
}
if ($fixtureReportText -notmatch 'DrawingToolsWriteQto=True') {
    throw 'Synthetic CAD validation did not prove that drawing tools write valid QTO data.'
}
if ($fixtureReportText -notmatch 'ReviewSelectedRepair=True') {
    throw 'Synthetic CAD validation did not prove that Review repairs only selected QTO objects.'
}
if ($fixtureReportText -notmatch 'ReviewRelationshipChecks=True') {
    throw 'Synthetic CAD validation did not prove the relationship and material Review rules.'
}
if ($fixtureReportText -notmatch 'LargePerformance=True') {
    throw 'Synthetic CAD validation did not pass the large-drawing performance baseline.'
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
    $zipValidationGuideEntry = $zipArchive.Entries | Where-Object {
        $_.FullName -eq 'docs/QTO_V1_0_BETA_VALIDATION.md' -or
        $_.FullName -eq 'docs\QTO_V1_0_BETA_VALIDATION.md'
    } | Select-Object -First 1
    if ($null -eq $zipValidationGuideEntry) { throw 'Installer ZIP does not contain the v1.0 Beta validation guide.' }
    $zipSecondPcChecklistEntry = $zipArchive.Entries | Where-Object {
        $_.FullName -eq 'docs/QTO_V1_0_SECOND_PC_CHECKLIST.md' -or
        $_.FullName -eq 'docs\QTO_V1_0_SECOND_PC_CHECKLIST.md'
    } | Select-Object -First 1
    if ($null -eq $zipSecondPcChecklistEntry) { throw 'Installer ZIP does not contain the second-PC validation checklist.' }
    foreach ($requiredEntry in @('install_or_update_QtoWirePlugin.bat', 'check_installation.bat')) {
        if (-not ($zipArchive.Entries | Where-Object { $_.FullName -eq $requiredEntry } | Select-Object -First 1)) {
            throw "Installer ZIP does not contain $requiredEntry."
        }
    }

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
if ($installerManifestText -notmatch 'AppVersion="1\.0\.0"') {
    throw 'Installer manifest version is not v1.0.0.'
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

if (-not [string]::IsNullOrWhiteSpace($BudgetSamplePath)) {
    if (-not (Test-Path -LiteralPath $BudgetSamplePath)) { throw "Budget sample does not exist: $BudgetSamplePath" }
    & $csc /nologo /target:exe /out:$budgetSmokeExe /reference:$pluginDll /reference:$acdb /reference:System.dll /reference:System.Core.dll $budgetSmokeSource
    if ($LASTEXITCODE -ne 0) { throw 'Budget mapping smoke compile failed.' }
    & $budgetSmokeExe $BudgetSamplePath
    if ($LASTEXITCODE -ne 0) { throw 'Budget mapping smoke failed.' }
}

Write-Host "Validation passed. Commands=$($sourceCommands.Count); Version=1.0.0-beta"
