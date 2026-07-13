param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'QtoWirePlugin.csproj'
$bundle = Join-Path $root 'QtoWirePlugin_v1.0.bundle'
$bundleWindows = Join-Path $bundle 'Contents\Windows'
$release = Join-Path $root 'release'
$installer = Join-Path $release 'QtoWirePlugin_v1.0.0-beta_installer'
$installerBundle = Join-Path $installer 'QtoWirePlugin.bundle'
$zip = Join-Path $release 'QtoWirePlugin_v1.0.0-beta_installer.zip'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe'

if (-not $SkipBuild) {
    & $msbuild $project /t:Rebuild /p:Configuration=Release /p:Platform=x64 /m
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}

New-Item -ItemType Directory -Path $bundleWindows -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'bin\x64\Release\QtoWirePlugin.dll') -Destination $bundleWindows -Force
Copy-Item -LiteralPath (Join-Path $root 'bin\x64\Release\QtoWirePlugin.pdb') -Destination $bundleWindows -Force

if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Recurse -Force }
New-Item -ItemType Directory -Path $installer -Force | Out-Null
Copy-Item -LiteralPath $bundle -Destination $installerBundle -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\install_or_update_QtoWirePlugin.bat') -Destination $installer -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\check_installation.bat') -Destination $installer -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\run_plugin_self_test.bat') -Destination $installer -Force
Copy-Item -LiteralPath (Join-Path $root 'installer\run_installed_self_test.ps1') -Destination $installer -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $installer -Force
New-Item -ItemType Directory -Path (Join-Path $installer 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs\QTO_V1_0_BETA_VALIDATION.md') -Destination (Join-Path $installer 'docs') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\QTO_V1_0_SECOND_PC_CHECKLIST.md') -Destination (Join-Path $installer 'docs') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\QTO_V1_0_COMPLETION_MATRIX.md') -Destination (Join-Path $installer 'docs') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\QTO_V1_0_UI_FLOW_MAP.md') -Destination (Join-Path $installer 'docs') -Force

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $installer '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Beta package created: $zip"
