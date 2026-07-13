param(
    [switch]$Installed,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixtureDirectory = Join-Path $root 'tests\fixtures'
$coreConsole = 'C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe'

if ($Installed) {
    $dll = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Autodesk\ApplicationPlugins\QtoWirePlugin.bundle\Contents\Windows\QtoWirePlugin.dll'
    $report = Join-Path $fixtureDirectory 'qto_installed_beta_report.txt'
}
else {
    $dll = Join-Path $root 'bin\x64\Release\QtoWirePlugin.dll'
    $report = Join-Path $fixtureDirectory 'qto_validation_report.txt'
}

foreach ($required in @($coreConsole, $dll)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required validation file is missing: $required"
    }
}

$scriptPath = Join-Path $fixtureDirectory 'run_core_validation.scr'
$scriptText = @(
    '_.NETLOAD',
    ($dll -replace '\\', '/'),
    'QTO_VALIDATE_VALIDATION_FIXTURE',
    '_.QUIT',
    '_N'
) -join "`r`n"
[IO.File]::WriteAllText($scriptPath, $scriptText, [Text.UTF8Encoding]::new($false))

$previousWriteTime = if (Test-Path -LiteralPath $report) { (Get-Item -LiteralPath $report).LastWriteTimeUtc } else { [datetime]::MinValue }
$env:QTO_FIXTURE_REPORT = $report
$process = Start-Process -FilePath $coreConsole -ArgumentList @('/s', $scriptPath, '/l', 'en-US') -PassThru -WindowStyle Hidden
$deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)

try {
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $reportReady = Test-Path -LiteralPath $report -PathType Leaf
        if ($reportReady) {
            $reportReady = (Get-Item -LiteralPath $report).LastWriteTimeUtc -gt $previousWriteTime
        }
    } while (-not $reportReady -and -not $process.HasExited -and [datetime]::UtcNow -lt $deadline)

    if (-not $reportReady) {
        throw "AutoCAD Core Console did not produce a new report within $TimeoutSeconds seconds."
    }

    $reportText = Get-Content -LiteralPath $report -Raw -Encoding utf8
    if ($reportText -notmatch 'Status=PASS') {
        throw "Core validation failed.`r`n$reportText"
    }

    Write-Host $reportText.TrimEnd()
}
finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        $process.WaitForExit(15000) | Out-Null
        $process.Refresh()
    }
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
    }
    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
}
