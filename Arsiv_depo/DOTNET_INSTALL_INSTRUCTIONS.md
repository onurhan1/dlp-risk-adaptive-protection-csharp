# .NET SDK Kurulum Talimatları

## ⚠️ Önemli: Sudo Şifresi Gerekli

.NET SDK kurulumu için **Mac sistem şifreniz** gerekiyor. Terminal'de şifre girmeniz istenecek.

---

## 🚀 Hızlı Kurulum (Önerilen)

### Terminal'de şu komutu çalıştırın:

```bash
brew install --cask dotnet-sdk@8
```

**Mac şifreniz istenecek** - şifrenizi girin ve Enter'a basın.

---

## 📦 Alternatif: İndirilmiş Paketi Kullanma

Eğer paket zaten indirilmişse (Homebrew cache'de):

```bash
# Paket yolunu bul
PACKAGE=$(find ~/Library/Caches/Homebrew/downloads -name "*dotnet-sdk*.pkg" | head -1)

# Kur (sudo şifresi istenecek)
sudo installer -pkg "$PACKAGE" -target /
```

---

## 🌐 Manuel İndirme

1. **Tarayıcıda açın**: https://dotnet.microsoft.com/download/dotnet/8.0
2. **macOS** için **.NET SDK 8.0** bölümüne gidin
3. **ARM64** (Apple Silicon) veya **x64** (Intel) seçin
4. İndirilen `.pkg` dosyasını çalıştırın
5. Kurulum sihirbazını takip edin

---

## ✅ Kurulum Kontrolü

Kurulum sonrası:

```bash
dotnet --version
# Beklenen: 8.0.xxx
```

---

## 🔧 PATH Sorunu

Eğer `dotnet --version` çalışmazsa:

```bash
# Geçici olarak PATH'e ekle
export PATH="/usr/local/share/dotnet:$PATH"

# Kalıcı olarak ekle (~/.zshrc veya ~/.bash_profile)
echo 'export PATH="/usr/local/share/dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

---

## 📝 Sonraki Adım

.NET SDK kurulduktan sonra:

```bash
cd "/Users/onurhany/Desktop/DLP_Automations/Risk Adaptive Protection CSharp"
./complete-setup.sh
```

Bu script otomatik olarak:
- NuGet paketlerini restore eder
- Projeleri build eder
- Entity Framework Tools kurar
- Database migration'ı çalıştırır

---

**Kurulum için Terminal'de şifrenizi girmeniz gerekecek! 🔐**
