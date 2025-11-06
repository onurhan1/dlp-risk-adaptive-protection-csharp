# GitHub Push Talimatları

## ✅ Repository Hazır!

Repository başarıyla oluşturuldu ve remote ayarlandı:
- **URL**: https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp
- **Remote**: origin (hem HTTPS hem SSH için yapılandırıldı)

## 🚀 Push İşlemi (3 Yöntem)

### Yöntem 1: Personal Access Token ile HTTPS (Önerilen)

1. **Personal Access Token Oluşturun**:
   - GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
   - "Generate new token (classic)" tıklayın
   - Token ismi: `dlp-push-token`
   - İzinler: ✅ `repo` (Full control of private repositories)
   - "Generate token" tıklayın
   - **Token'ı kopyalayın** (bir daha gösterilmeyecek!)

2. **Push Yapın**:
```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"

# HTTPS remote kullan
git remote set-url origin https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git

# Push (kullanıcı adı: onurhan1, şifre yerine token kullanın)
git push -u origin main
# Username: onurhan1
# Password: [Personal Access Token'ı yapıştırın]
```

### Yöntem 2: SSH ile (SSH Key varsa)

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"

# SSH remote kullan
git remote set-url origin git@github.com:onurhan1/dlp-risk-adaptive-protection-csharp.git

# Push
git push -u origin main
```

**SSH Key yoksa**:
```bash
# SSH key oluştur
ssh-keygen -t ed25519 -C "your_email@example.com"

# Public key'i GitHub'a ekle
cat ~/.ssh/id_ed25519.pub
# GitHub → Settings → SSH and GPG keys → New SSH key → Key'i yapıştırın
```

### Yöntem 3: GitHub CLI ile (Eğer kuruluysa)

```bash
# GitHub CLI ile login
gh auth login

# Push
git push -u origin main
```

## 📋 Mevcut Durum

- ✅ Remote repository ayarlandı
- ✅ 3 commit hazır (Initial commit, docs, scripts)
- ✅ Tüm dosyalar commit edildi
- ⏳ Push için authentication gerekli

## 🔍 Hızlı Komut

**En kolay yöntem** - Personal Access Token:

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
git remote set-url origin https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp.git
git push -u origin main
```

Username: `onurhan1`
Password: `[Personal Access Token]`

## ✅ Push Başarılı Olduğunda

Repository'niz şu adreste olacak:
**https://github.com/onurhan1/dlp-risk-adaptive-protection-csharp**

Artık yaptığımız her değişikliği şu şekilde push edebiliriz:

```bash
git add .
git commit -m "Açıklayıcı mesaj"
git push origin main
```

Veya push script'ini kullanın:
```bash
./push-to-github.sh
```

