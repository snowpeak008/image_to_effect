param(
    [Parameter(Mandatory = $true)]
    [string]$BlindRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$tileWidth = 320
$imageHeight = 180
$labelHeight = 26
$tileHeight = $imageHeight + $labelHeight
$columns = 4
$rows = 6
$headerHeight = 42
$sheetWidth = $columns * $tileWidth
$sheetHeight = $headerHeight + ($rows * $tileHeight)

$font = [System.Drawing.Font]::new('Consolas', 10, [System.Drawing.FontStyle]::Regular)
$headerFont = [System.Drawing.Font]::new('Consolas', 15, [System.Drawing.FontStyle]::Bold)
$whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$grayBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 210, 210, 210))
$missingBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 20, 20))
$borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 90, 90, 90), 1)
$missingPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 255, 80, 80), 5)

try {
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $BlindRoot 'blind-submission-manifest.json') | ConvertFrom-Json
    foreach ($sample in ($manifest.samples | Sort-Object sampleId)) {
        $evidencePath = Join-Path $BlindRoot ([string]$sample.evidenceManifest)
        $evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
        $frames = @($evidence.frames | Sort-Object seedOrdinal, frameIndex)
        if ($frames.Count -ne 24) {
            throw "Expected 24 frame records for $($sample.sampleId), found $($frames.Count)"
        }

        $bitmap = [System.Drawing.Bitmap]::new($sheetWidth, $sheetHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 18, 18, 18))
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawString("$($sample.sampleId) | 3 seeds x 8 fixed Beauty frames", $headerFont, $whiteBrush, 10, 8)

            for ($i = 0; $i -lt $frames.Count; $i++) {
                $frame = $frames[$i]
                $seed = [int]$frame.seedOrdinal
                $withinSeed = $i % 8
                $column = $withinSeed % 4
                $row = ($seed * 2) + [math]::Floor($withinSeed / 4)
                $x = $column * $tileWidth
                $y = $headerHeight + ($row * $tileHeight)
                $imageRect = [System.Drawing.Rectangle]::new($x, $y, $tileWidth, $imageHeight)
                $labelRect = [System.Drawing.RectangleF]::new($x + 4, $y + $imageHeight + 4, $tileWidth - 8, $labelHeight - 4)
                $availability = [string]$frame.beauty.availability
                $frameFile = [string]$frame.beauty.file
                $resolvedFrame = if ([string]::IsNullOrWhiteSpace($frameFile)) { $null } else { Join-Path $BlindRoot $frameFile }
                $present = ($availability -eq 'present') -and $resolvedFrame -and (Test-Path -LiteralPath $resolvedFrame)

                if ($present) {
                    $source = [System.Drawing.Image]::FromFile($resolvedFrame)
                    try {
                        $graphics.DrawImage($source, $imageRect)
                    }
                    finally {
                        $source.Dispose()
                    }
                }
                else {
                    $graphics.FillRectangle($missingBrush, $imageRect)
                    $graphics.DrawLine($missingPen, $x + 12, $y + 12, $x + $tileWidth - 12, $y + $imageHeight - 12)
                    $graphics.DrawLine($missingPen, $x + $tileWidth - 12, $y + 12, $x + 12, $y + $imageHeight - 12)
                    $graphics.DrawString('MISSING / UNREADABLE', $headerFont, $whiteBrush, $x + 43, $y + 75)
                }

                $graphics.DrawRectangle($borderPen, $imageRect)
                $label = ('seed {0} | frame {1:D3} | {2} | {3}' -f $seed, [int]$frame.frameIndex, [string]$frame.stateRef, $(if ($present) { 'READ' } else { $availability.ToUpperInvariant() }))
                $graphics.DrawString($label, $font, $(if ($present) { $grayBrush } else { $whiteBrush }), $labelRect)
            }

            $outputPath = Join-Path $OutputDirectory ("$($sample.sampleId).png")
            $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $font.Dispose()
    $headerFont.Dispose()
    $whiteBrush.Dispose()
    $grayBrush.Dispose()
    $missingBrush.Dispose()
    $borderPen.Dispose()
    $missingPen.Dispose()
}

$sheets = @(Get-ChildItem -LiteralPath $OutputDirectory -File -Filter '*.png')
if ($sheets.Count -ne 66) {
    throw "Expected 66 contact sheets, found $($sheets.Count)"
}

[pscustomobject]@{
    ContactSheetCount = $sheets.Count
    OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
} | ConvertTo-Json
