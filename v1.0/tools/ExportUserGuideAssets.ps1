param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\docs\user-guide-assets')
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pluginPath = Join-Path $projectRoot 'bin\x64\Release\QtoWirePlugin.dll'
$acadRoot = 'C:\Program Files\Autodesk\AutoCAD 2023'

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

foreach ($name in @('AcCoreMgd.dll', 'AcDbMgd.dll', 'AcMgd.dll', 'AdWindows.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $acadRoot $name)) | Out-Null
}
$plugin = [Reflection.Assembly]::LoadFrom($pluginPath)

function Save-RibbonIcon {
    param([string]$CommandName)

    $type = $plugin.GetType('QtoWirePlugin.QtoRibbon', $true)
    $flags = [Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static
    $method = $type.GetMethod('CreateRibbonIcon', $flags)
    $bitmap = $method.Invoke($null, @($CommandName, 64))
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $path = Join-Path $OutputDirectory ($CommandName.ToLowerInvariant() + '.png')
    $stream = [IO.File]::Open($path, [IO.FileMode]::Create)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

function Save-ControlScreenshot {
    param(
        [System.Windows.Forms.Control]$Control,
        [string]$FileName,
        [int]$Width,
        [int]$Height
    )

    if ($Control -is [System.Windows.Forms.Form]) {
        $hostForm = [System.Windows.Forms.Form]$Control
        $hostForm.ClientSize = New-Object System.Drawing.Size($Width, $Height)
    }
    else {
        $hostForm = New-Object System.Windows.Forms.Form
        $hostForm.ClientSize = New-Object System.Drawing.Size($Width, $Height)
        $hostForm.Text = $Control.Text
        $Control.Dock = [System.Windows.Forms.DockStyle]::Fill
        $hostForm.Controls.Add($Control)
    }
    $hostForm.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $hostForm.Location = New-Object System.Drawing.Point(-30000, -30000)
    $hostForm.ShowInTaskbar = $false
    $hostForm.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $bitmap = New-Object System.Drawing.Bitmap($hostForm.Width, $hostForm.Height)
    try {
        $hostForm.DrawToBitmap($bitmap, (New-Object System.Drawing.Rectangle(0, 0, $hostForm.Width, $hostForm.Height)))
        $bitmap.Save((Join-Path $OutputDirectory $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
        $hostForm.Close()
        $hostForm.Dispose()
    }
}

$commands = @(
    'QTO_PROJECT_SETUP', 'QTO_WORKFLOW_PANEL', 'QTO_CREATE_CONNECTIONS',
    'QTO_INSERT_CATALOG_BLOCK', 'QTO_BLOCK_LIBRARY_MANAGER', 'QTO_UPDATE_PROJECT_BLOCKS',
    'QTO_PLACE_CATALOG_CONTINUOUS', 'QTO_DRAW_QTO_PATH', 'QTO_CONVERT_LEGACY_OBJECTS',
    'QTO_PANEL', 'QTO_SYNC_FULL_REBUILD', 'QTO_VALIDATE',
    'QTO_SCOPE_DEFINE_FLOOR', 'QTO_SCOPE_DEFINE_SYSTEM', 'QTO_SCOPE_CLEAR',
    'QTO_PROPERTY_PANEL', 'QTO_BUDGET_MAPPING', 'QTO_BUDGET_COMPLETENESS',
    'QTO_MARK_OUTLETS', 'QTO_LABEL_OUTLETS', 'QTO_CALLOUT_TO_JB',
    'QTO_EDIT_OUTLET_PROPERTIES', 'QTO_DELETE_OUTLET_INFO',
    'QTO_MARK_JB', 'QTO_EDIT_JB_PROPERTIES', 'QTO_DELETE_JB_INFO',
    'QTO_MARK_TRAY', 'QTO_BATCH_ROUTE_BY_TRAY', 'QTO_CONDUIT_TO_TRAY', 'QTO_CLEAR_TRAY_PROPERTIES',
    'QTO_EXPORT_OUTLETS', 'QTO_CHECK_WIRE', 'QTO_EXPORT_JB_SUMMARY', 'QTO_EXPORT_CONDUIT_SUMMARY'
)
foreach ($command in $commands) { Save-RibbonIcon -CommandName $command }

$workflow = [Activator]::CreateInstance($plugin.GetType('QtoWirePlugin.QtoWorkflowForm', $true))
Save-ControlScreenshot -Control $workflow -FileName 'workflow-panel.png' -Width 960 -Height 720

$review = [Activator]::CreateInstance($plugin.GetType('QtoWirePlugin.QtoReviewForm', $true))
Save-ControlScreenshot -Control $review -FileName 'review-list.png' -Width 980 -Height 640

$loader = $plugin.GetType('QtoWirePlugin.QtoDictionaryLoader', $true)
$dictionary = $loader.GetMethod('LoadDefault').Invoke($null, @($null))
$propertyPanel = [Activator]::CreateInstance($plugin.GetType('QtoWirePlugin.QtoPropertyPanelForm', $true), @($dictionary))
Save-ControlScreenshot -Control $propertyPanel -FileName 'property-panel.png' -Width 430 -Height 820

try {
    $syncPanel = [Activator]::CreateInstance($plugin.GetType('QtoWirePlugin.QtoSyncMainPalette', $true))
    Save-ControlScreenshot -Control $syncPanel -FileName 'sync-control.png' -Width 900 -Height 760
}
catch {
    Write-Warning 'Sync control requires an AutoCAD host and was not exported.'
}

$sampleCatalog = Join-Path $projectRoot 'tests\fixtures\sample.qto_catalog.json'
try {
    $picker = [Activator]::CreateInstance($plugin.GetType('QtoWirePlugin.QtoBlockLibraryPickerForm', $true), @($sampleCatalog))
    Save-ControlScreenshot -Control $picker -FileName 'block-picker.png' -Width 900 -Height 620
}
catch {
    Write-Warning 'Block picker could not be exported outside AutoCAD.'
}

Write-Host "User guide assets exported to $OutputDirectory"
