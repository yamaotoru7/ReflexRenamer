# Reflex Renamer のアプリアイコン（濃いスレートに白い A とオレンジの B、A→B の矢印）を各サイズでベクター描画し、
# PNG 圧縮の .ico にまとめる。16〜24px は矢印を省いて A と B だけにする（潰れて汚れに見えるため）。
# 使い方（リポジトリ直下）: pwsh tools/make-app-icon.ps1 src/ReflexRenamer/Assets/Icons/app.ico
# 確認用の PNG（app-<size>.png、app-sheet.png）はスクリプトと同じフォルダに出る。
param([Parameter(Mandatory)][string]$OutPath)
Add-Type -AssemblyName System.Drawing

function Col($hex) { return [System.Drawing.ColorTranslator]::FromHtml($hex) }
function RoundRect([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $d = $r * 2
  $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90); $p.CloseFigure(); return $p
}
function Arrow($g, $color, [double]$x1, [double]$y1, [double]$x2, [double]$y2, [double]$w) {
  $pen = New-Object System.Drawing.Pen $color, $w; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
  $g.DrawLine($pen, $x1, $y1, $x2, $y2)
  $dx = $x2 - $x1; $dy = $y2 - $y1; $len = [math]::Sqrt($dx*$dx + $dy*$dy); $ux = $dx / $len; $uy = $dy / $len
  $h = $w * 2.2
  $g.DrawLine($pen, $x2 - $ux*$h - $uy*$h*0.8, $y2 - $uy*$h + $ux*$h*0.8, $x2, $y2)
  $g.DrawLine($pen, $x2 - $ux*$h + $uy*$h*0.8, $y2 - $uy*$h - $ux*$h*0.8, $x2, $y2)
}
function DrawText($g, $text, $font, $color, [double]$x, [double]$y, [double]$w, [double]$h) {
  $f = New-Object System.Drawing.StringFormat; $f.Alignment = 'Center'; $f.LineAlignment = 'Center'
  $g.DrawString($text, $font, (New-Object System.Drawing.SolidBrush $color), (New-Object System.Drawing.RectangleF $x, $y, $w, $h), $f)
}

# 256 の座標系で描き、size に合わせて拡大縮小する。小さいサイズは文字を少し大きく・矢印を太くして読めるようにする
function Render([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.PixelOffsetMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'
  $g.ScaleTransform($size / 256.0, $size / 256.0)
  $small = $size -le 24
  $radius = if ($small) { 40 } else { 56 }
  $margin = if ($small) { 0 } else { 8 }
  $path = RoundRect $margin $margin (256 - $margin * 2) (256 - $margin * 2) $radius
  $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 256, 256), (Col '#334155'), (Col '#0F172A')
  $g.FillPath($br, $path)
  $fontPx = if ($small) { 150 } elseif ($size -le 48) { 132 } else { 120 }
  $font = New-Object System.Drawing.Font 'Segoe UI Variable Display', $fontPx, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  if ($small) {
    # 16〜24px：矢印を省き、A と B だけを大きく（矢印は潰れて汚れに見える）
    DrawText $g 'A' $font (Col '#E2E8F0') 0 50 132 160
    DrawText $g 'B' $font (Col '#FB923C') 124 50 132 160
  } else {
    DrawText $g 'A' $font (Col '#CBD5E1') 14 60 100 140
    $arrowWidth = if ($size -le 48) { 18 } else { 14 }
    Arrow $g (Col '#FB923C') 108 128 148 128 $arrowWidth
    DrawText $g 'B' $font (Col '#FB923C') 142 60 100 140
  }
  $g.Dispose()
  return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
  $bmp = Render $s
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += ,($ms.ToArray())
  $bmp.Save((Join-Path $PSScriptRoot "app-$s.png"))
  $bmp.Dispose()
}

# ICO: ICONDIR(6) + ICONDIRENTRY(16)*n + データ
$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $data = $pngs[$i]
  $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
  $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32)
  $bw.Write([uint32]$data.Length); $bw.Write([uint32]$offset)
  $offset += $data.Length
}
foreach ($data in $pngs) { $bw.Write($data) }
$bw.Dispose(); $fs.Dispose()
"saved $OutPath ($($sizes -join ','))"

# 確認用：ライト／ダーク背景に各サイズを並べる
$sheet = New-Object System.Drawing.Bitmap 640, 180
$sg = [System.Drawing.Graphics]::FromImage($sheet); $sg.InterpolationMode = 'NearestNeighbor'
$sg.FillRectangle((New-Object System.Drawing.SolidBrush (Col '#F3F4F6')), 0, 0, 640, 90)
$sg.FillRectangle((New-Object System.Drawing.SolidBrush (Col '#202020')), 0, 90, 640, 90)
$x = 12
foreach ($s in 16, 20, 24, 32, 40, 48, 64, 128) {
  $img = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "app-$s.png"))
  $sg.DrawImage($img, $x, 70 - $s, $s, $s); $sg.DrawImage($img, $x, 160 - $s, $s, $s)
  $x += $s + 16; $img.Dispose()
}
$sheet.Save((Join-Path $PSScriptRoot 'app-sheet.png'))
