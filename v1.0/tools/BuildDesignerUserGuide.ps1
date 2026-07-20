param(
    [string]$SourceHtml = "",
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($SourceHtml)) {
    $SourceHtml = Join-Path $projectRoot "docs\QTO_v1.0.1_designer_user_guide.html"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "docs"
}

$sourcePath = (Resolve-Path -LiteralPath $SourceHtml).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

$baseName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)
$docxPath = Join-Path $outputPath ($baseName + ".docx")
$pdfPath = Join-Path $outputPath ($baseName + ".pdf")

function Save-DocxFromHtml {
    param(
        [string]$HtmlPath,
        [string]$DestinationPath
    )

    $word = $null
    $document = $null

    try {
        $word = New-Object -ComObject Word.Application
        $word.Visible = $false
        $word.DisplayAlerts = 0
        $document = $word.Documents.Open($HtmlPath, $false, $true)

        for ($index = $document.InlineShapes.Count; $index -ge 1; $index--) {
            $inlineShape = $null
            try {
                $inlineShape = $document.InlineShapes.Item($index)
                $inlineShape.LinkFormat.SavePictureWithDocument = $true
                $inlineShape.LinkFormat.BreakLink()
            }
            catch {
                # An already embedded image has no link to break.
            }
            finally {
                if ($null -ne $inlineShape) {
                    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($inlineShape)
                }
            }
        }

        $document.SaveAs2($DestinationPath, 16)
    }
    finally {
        if ($null -ne $document) {
            $document.Close($false)
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($document)
        }

        if ($null -ne $word) {
            $word.Quit()
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($word)
        }

        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
    }
}

function Save-PdfFromHtml {
    param(
        [string]$HtmlPath,
        [string]$DestinationPath
    )

    $edgeCandidates = @(
        "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    )
    $edgePath = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($edgePath)) {
        throw "Microsoft Edge was not found."
    }

    $htmlUri = [System.Uri]::new($HtmlPath).AbsoluteUri
    $profilePath = Join-Path $projectRoot "tmp\edge-guide-profile"
    [System.IO.Directory]::CreateDirectory($profilePath) | Out-Null

    $arguments = @(
        "--headless=new",
        "--disable-gpu",
        "--no-sandbox",
        "--allow-file-access-from-files",
        ("--user-data-dir=" + $profilePath),
        "--no-pdf-header-footer",
        ("--print-to-pdf=" + $DestinationPath),
        $htmlUri
    )

    $process = Start-Process -FilePath $edgePath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $DestinationPath)) {
        throw "Microsoft Edge failed to create the PDF."
    }
}

$docxStatus = "not created"
try {
    Save-DocxFromHtml -HtmlPath $sourcePath -DestinationPath $docxPath
    $docxStatus = $docxPath
}
catch {
    Write-Warning ("DOCX conversion was skipped: " + $_.Exception.Message)
}

$pdfStatus = "not created"
try {
    Save-PdfFromHtml -HtmlPath $sourcePath -DestinationPath $pdfPath
    $pdfStatus = $pdfPath
}
catch {
    Write-Warning ("PDF conversion failed: " + $_.Exception.Message)
}

Write-Output ("HTML: " + $sourcePath)
Write-Output ("DOCX: " + $docxStatus)
Write-Output ("PDF: " + $pdfStatus)
