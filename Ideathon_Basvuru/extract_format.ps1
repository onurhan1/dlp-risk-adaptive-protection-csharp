$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open("c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Ideathon_Basvuru\Emanet_Basvuru_Final_Draft_1.docx")

$output = @()

foreach ($para in $doc.Paragraphs) {
    $range = $para.Range
    $text = $range.Text.Trim()
    if ($text.Length -eq 0) { continue }
    
    $preview = if ($text.Length -gt 80) { $text.Substring(0, 80) + "..." } else { $text }
    
    $fontName = $range.Font.Name
    $fontSize = $range.Font.Size
    $bold = $range.Font.Bold
    $italic = $range.Font.Italic
    $color = $range.Font.Color
    $alignment = $para.Alignment
    $spaceBefore = $para.SpaceBefore
    $spaceAfter = $para.SpaceAfter
    $lineSpacing = $para.LineSpacing
    $styleName = $para.Style.NameLocal
    
    $line = "STYLE=[$styleName] FONT=[$fontName] SIZE=[$fontSize] BOLD=[$bold] ITALIC=[$italic] COLOR=[$color] ALIGN=[$alignment] SB=[$spaceBefore] SA=[$spaceAfter] LS=[$lineSpacing] TEXT=[$preview]"
    $output += $line
}

# Also get page margins
$sec = $doc.Sections.Item(1)
$output += ""
$output += "=== PAGE SETUP ==="
$output += "TopMargin=$($sec.PageSetup.TopMargin)"
$output += "BottomMargin=$($sec.PageSetup.BottomMargin)"
$output += "LeftMargin=$($sec.PageSetup.LeftMargin)"
$output += "RightMargin=$($sec.PageSetup.RightMargin)"
$output += "PageWidth=$($sec.PageSetup.PageWidth)"
$output += "PageHeight=$($sec.PageSetup.PageHeight)"

$output | Out-File -FilePath "c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Ideathon_Basvuru\format_details.txt" -Encoding UTF8

$doc.Close()
$word.Quit()
Write-Host "Done"
