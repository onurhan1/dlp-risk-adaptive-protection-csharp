<#
.SYNOPSIS
    Forcepoint DLP API'den son olaylari ceken ve JSON olarak kaydeden test scripti.
    Source alanindaki DN, DisplayName vb. alanlari kontrol etmek icin kullanilir.

.EXAMPLE
    .\fetch-dlp-incidents.ps1 -ManagerIP "10.1.1.50" -Username "admin" -Password "sifre123"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ManagerIP,

    [Parameter(Mandatory=$false)]
    [int]$ManagerPort = 8443,

    [Parameter(Mandatory=$true)]
    [string]$Username,

    [Parameter(Mandatory=$true)]
    [string]$Password,

    [Parameter(Mandatory=$false)]
    [int]$Hours = 24,

    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "dlp-response.json"
)

# HTTPS sertifika hatalarini yoksay (Self-signed sertifikalar icin)
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$baseUrl = "https://$($ManagerIP):$($ManagerPort)/dlp/rest/v1"

Write-Host "1. Baglanti kuruluyor: $baseUrl" -ForegroundColor Cyan

# 1. Access Token Al
$authUrl = "$baseUrl/auth/access-token"
$headers = @{
    "username" = $Username
    "password" = $Password
}

try {
    Write-Host "   Token isteniyor..."
    $authResponse = Invoke-RestMethod -Uri $authUrl -Method Post -Headers $headers -ErrorAction Stop
    
    # Token farkli formatlarda gelebilir (access_token veya accessToken)
    $token = $null
    if ($authResponse.access_token) { $token = $authResponse.access_token }
    elseif ($authResponse.accessToken) { $token = $authResponse.accessToken }
    elseif ($authResponse.token) { $token = $authResponse.token }

    if (-not $token) {
        Write-Error "Token alinamadi. Yanit: $($authResponse | ConvertTo-Json)"
        exit
    }

    Write-Host "   Basarili! Token alindi." -ForegroundColor Green
}
catch {
    Write-Error "Kimlik dogrulama hatasi: $_"
    exit
}

# 2. Olaylari Çek
$incidentsUrl = "$baseUrl/incidents/"
$endTime = Get-Date
$startTime = $endTime.AddHours(-$Hours)

# Tarih formati: dd/MM/yyyy HH:mm:ss
$body = @{
    type = "INCIDENTS"
    from_date = $startTime.ToString("dd/MM/yyyy HH:mm:ss")
    to_date = $endTime.ToString("dd/MM/yyyy HH:mm:ss")
} | ConvertTo-Json

$authHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

try {
    Write-Host "2. Son $Hours saatin olaylari cekiliyor..." -ForegroundColor Cyan
    Write-Host "   Istek: $body"
    
    $incidentResponse = Invoke-RestMethod -Uri $incidentsUrl -Method Post -Headers $authHeaders -Body $body -ErrorAction Stop
    
    # JSON olarak kaydet
    $jsonOutput = $incidentResponse | ConvertTo-Json -Depth 10
    $jsonOutput | Out-File -FilePath $OutputFile -Encoding utf8

    $count = 0
    if ($incidentResponse.incidents) { $count = $incidentResponse.incidents.Count }
    
    Write-Host "   Basarili! $count adet olay cekildi." -ForegroundColor Green
    Write-Host "   Detayli JSON dosyasi olusturuldu: $OutputFile" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "3. Örnek Source Analizi:" -ForegroundColor Cyan
    
    if ($count -gt 0) {
        # Ilk 3 olayin source kismini ekrana yazdir
        $incidentResponse.incidents | Select-Object -First 3 | ForEach-Object {
            Write-Host "   --- Incident ID: $($_.id) ---"
            if ($_.source) {
                Write-Host "   Source Properties:"
                $_.source | Get-Member -MemberType NoteProperty | ForEach-Object {
                    $propName = $_.Name
                    $propVal = $_.Definition.Split('=')[1] # Degeri almak icin basit parse (Not perfect in PS object)
                    # PS Object propertysini direkt yazdiralim
                    $val = $null
                    try { $val = $_.source.$propName } catch {} 
                    
                    # Alternatif: Hashtable ise
                    if ($_.source -is [System.Collections.Hashtable]) {
                         foreach ($key in $_.source.Keys) {
                             Write-Host "     $key : $($_.source[$key])"
                         }
                    } else {
                        # PSUserObject ise (Invoke-RestMethod donusu)
                        Write-Host "     Login Name : $($_.source.login_name)"
                        Write-Host "     Display Name : $($_.source.display_name)"
                        Write-Host "     User Name : $($_.source.user_name)"
                        Write-Host "     DN : $($_.source.dn)"
                        Write-Host "     Full Object : $($_.source | ConvertTo-Json -Depth 1 -Compress)"
                    }
                }
            } else {
                Write-Host "     Source bilgisi YOK."
            }
            Write-Host ""
        }
    } else {
        Write-Host "   Olay bulunamadi."
    }

}
catch {
    Write-Error "Olay çekme hatasi: $_"
    # Detayli hata
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Error "Sunucu Yaniti: $($reader.ReadToEnd())"
    }
}
