$ErrorActionPreference = 'Stop'
$coreConsole = 'C:\Program Files\Autodesk\AutoCAD 2023\accoreconsole.exe'
$bundle = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Autodesk\ApplicationPlugins\QtoWirePlugin.bundle'
$dll = Join-Path $bundle 'Contents\Windows\QtoWirePlugin.dll'
$workDirectory = Join-Path ([IO.Path]::GetTempPath()) 'QtoWirePluginSelfTest'
$report = Join-Path $workDirectory 'qto_self_test_report.txt'
$scriptPath = Join-Path $workDirectory 'qto_self_test.scr'

foreach ($required in @($coreConsole, $dll)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required file is missing: $required"
    }
}

New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
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
$deadline = [datetime]::UtcNow.AddSeconds(90)

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
        throw 'AutoCAD did not produce a self-test report within 90 seconds.'
    }

    $reportText = Get-Content -LiteralPath $report -Raw -Encoding utf8
    if ($reportText -notmatch 'Status=PASS') {
        throw "QtoWirePlugin self-test failed.`r`n$reportText"
    }

    Write-Host 'QtoWirePlugin self-test passed.'
    Write-Host $reportText.TrimEnd()
    Write-Host "Report: $report"
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
