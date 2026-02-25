# GitHub Repository Kurulum Rehberi

## 🚀 Otomatik Kurulum (GitHub CLI ile)

Eğer GitHub CLI (gh) kuruluysa, aşağıdaki komutu çalıştırın:

```bash
gh repo create dlp-risk-adaptive-protection-csharp \
  --private \
  --source=. \
  --remote=origin \
  --description "Forcepoint Risk Adaptive Protection - C# Windows Native Implementation"

git push -u origin main
```

## 📝 Manuel Kurulum

### 1. GitHub'da Repository Oluşturma

1. GitHub'a giriş yapın: https://github.com
2. Sağ üstteki **"+"** ikonuna tıklayın → **"New repository"**
3. Repository ayarları:
   - **Repository name**: `dlp-risk-adaptive-protection-csharp`
   - **Description**: `Forcepoint Risk Adaptive Protection - C# Windows Native Implementation`
   - **Visibility**: ✅ **Private** seçin
   - **Initialize**: ❌ README, .gitignore, license **işaretlemeyin** (zaten mevcut)
4. **"Create repository"** butonuna tıklayın

### 2. Local Repository'yi GitHub'a Bağlama

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"

# Remote repository ekle (YOUR_USERNAME'i kendi kullanıcı adınızla değiştirin)
git remote add origin https://github.com/YOUR_USERNAME/dlp-risk-adaptive-protection-csharp.git

# Veya SSH kullanıyorsanız:
git remote add origin git@github.com:YOUR_USERNAME/dlp-risk-adaptive-protection-csharp.git

# Mevcut branch'i kontrol edin
git branch -M main

# İlk push
git push -u origin main
```

### 3. Kullanıcı Adı/Email Ayarlama (İlk kez ise)

```bash
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

## 🔄 Değişiklikleri Push Etme

Her değişiklikten sonra:

```bash
# Değişiklikleri stage'e ekle
git add .

# Commit oluştur
git commit -m "Açıklayıcı commit mesajı"

# GitHub'a push et
git push origin main
```

## 📋 Mevcut Durum

Repository durumunu kontrol etmek için:

```bash
# Remote repository'yi kontrol et
git remote -v

# Branch durumunu kontrol et
git branch -a

# Son commit'leri görüntüle
git log --oneline -10
```

## 🔐 Güvenlik Notları

**ÖNEMLİ**: `.gitignore` dosyası şunları exclude ediyor:
- ✅ `appsettings.json` (hassas bilgiler içerir)
- ✅ `.env` dosyaları
- ✅ `node_modules/`
- ✅ `bin/`, `obj/` (build çıktıları)
- ✅ `reports/` (PDF dosyaları)
- ✅ Log dosyaları

**Production için**:
- Hassas bilgileri environment variables olarak saklayın
- `appsettings.example.json` şablon dosyasını kullanın
- GitHub Secrets kullanarak CI/CD'de şifreleri yönetin

## 🐛 Sorun Giderme

### "remote origin already exists"
```bash
git remote remove origin
git remote add origin https://github.com/YOUR_USERNAME/dlp-risk-adaptive-protection-csharp.git
```

### "Permission denied"
- GitHub'a SSH key eklenmiş mi kontrol edin
- Veya HTTPS kullanın ve Personal Access Token kullanın

### "Large file" hatası
```bash
# Büyük dosyaları kontrol edin
git ls-files | xargs du -h | sort -h | tail -20

# .gitignore'a ekleyin ve commit'ten kaldırın
git rm --cached large-file.zip
```

## 📚 GitHub CLI Kurulumu (Opsiyonel)

Mac:
```bash
brew install gh
gh auth login
```

Windows:
```powershell
winget install GitHub.cli
gh auth login
```

## ✅ Tamamlandı!

Repository başarıyla oluşturuldu ve push edildi!

**Repository URL**: `https://github.com/YOUR_USERNAME/dlp-risk-adaptive-protection-csharp`

