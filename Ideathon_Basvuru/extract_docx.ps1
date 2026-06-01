$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open("c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Ideathon_Basvuru\Emanet_Basvuru_Final_Draft_1.docx")
$text = $doc.Content.Text
$doc.Close()
$word.Quit()
$text | Out-File -FilePath "c:\Users\abdul\Desktop\dlp-risk-adaptive-protection-csharp-main\Ideathon_Basvuru\extracted_content.txt" -Encoding UTF8
Write-Host "Done"
