# DLP.RiskAnalyzer.Tests

Bu projeyi yerel makinenize kopyalayın ve aşağıdaki komutla çalıştırın.

## Gereksinimler

- .NET 8 SDK  
- Hedef sunucuda PostgreSQL veya Redis **gerekmez** — testler EF Core InMemory kullanır.

## Çalıştırma

```bash
# Proje klasöründe:
dotnet restore
dotnet test

# Sadece Unit testleri:
dotnet test --filter "FullyQualifiedName~Unit"

# Sadece Integration testleri:
dotnet test --filter "FullyQualifiedName~Integration"

# Detaylı çıktı:
dotnet test --logger "console;verbosity=detailed"
```

## Test Yapısı

```
DLP.RiskAnalyzer.Tests/
├── Unit/
│   ├── RiskScoringTests.cs            — Kod inceleme T-01: Risk formülü doğrulaması
│   ├── ViolationTriggerParserTests.cs — Kod inceleme M-02: Parser utility testleri
│   └── IncidentResponseMapperTests.cs — Kod inceleme M-01: Factory mapping testleri
└── Integration/
    └── UserListPaginationTests.cs     — Kod inceleme P-01 + T-01: DB-level pagination
```

## Kapsanan Kod İnceleme Sorunları

| Test Dosyası | Ele Alınan Sorunlar |
|---|---|
| `RiskScoringTests.cs` | T-01 (test yok), doğrudan güvenlik-kritik formüller |
| `ViolationTriggerParserTests.cs` | M-02 (tekrarlayan JSON parsing) |
| `IncidentResponseMapperTests.cs` | M-01 (duplicated mapping block) |
| `UserListPaginationTests.cs` | P-01 (bellek içi pagination), T-01 |
