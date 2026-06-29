# REFERENCE.md İhlal Düzeltmeleri — Görev Takibi

## Faz A — Boş Catch Blokları (§15)
- [x] A.1 `ReleasedIncidentProcessor.cs` — bare catch'lere log ekle
- [x] A.2 `BehaviorAIExplanationService.cs` — bare catch'lere log ekle
- [x] A.3 `ViolationTriggerParser.cs` / `RiskAnalyzerService.cs` — intentional comment ekle

## Faz B — Interface Eksiklikleri (§4)
- [x] B.1 Interface dosyaları oluştur (7 adet)
- [x] B.2 `ServiceCollectionExtensions.cs` DI kaydını güncelle
- [x] B.3 Controller'lardaki concrete referansları interface'e çevir

## Faz C — Şişman Dosya Bölünmesi (§2)
- [ ] C.1 `RiskController.cs` → `RiskIncidentsController.cs` bölünmesi
- [ ] C.2 `RiskAnalyzerService.cs` → `RiskScoringService.cs` bölünmesi
- [ ] C.3 Concrete `RiskAnalyzerService` referanslarını `IRiskAnalyzerService`'e çevir

## Doğrulama
- [ ] `dotnet build` → 0 hata
- [ ] `dotnet test` → 251/251 geçmeli
