# LotusCode Assignment - Fault Report Management API

Bu proje, elektrik dağıtım şirketi senaryosu için geliştirdiğim bir **.NET 8 Web API** çalışmasıdır. Temel amaç; arıza kayıtlarını güvenli, kurallı ve sürdürülebilir bir şekilde yönetmek.

Bu README’de şunları bulacaksın:
- Projeyi nasıl ayağa kaldıracağın
- Mimarinin nasıl kurgulandığı
- Hangi kütüphaneleri kullandığım
- Kodlama standartlarım ve iş kuralları
- Tüm endpointlerin açıklaması

---

## 1) Proje Özeti

API’nin sunduğu ana özellikler:

- JWT ile kimlik doğrulama
- Role-based yetkilendirme (`Admin`, `User`)
- Arıza kaydı CRUD operasyonları
- Durum (status) geçiş politikası
- Aynı lokasyon için 1 saat içinde tekrar kayıt engeli
- Global exception handling
- Standart API response formatı (`ApiResponse<T>`)
- Swagger/OpenAPI dokümantasyonu
- Serilog ile loglama
- SQL Server + EF Core
- Seed data (admin/user + örnek kayıtlar)
- Unit testler
- Rate limiting

---

## 2) Kullanılan Teknolojiler ve Kütüphaneler

### Platform
- `.NET 8`
- `ASP.NET Core Web API`

### Veri Erişimi
- `Entity Framework Core`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.EntityFrameworkCore.Tools`

### Kimlik Doğrulama / Güvenlik
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Identity` (password hashing için)

### Validasyon
- `FluentValidation`
- `FluentValidation.AspNetCore`

### Dokümantasyon
- `Swashbuckle.AspNetCore` (Swagger UI)

### Loglama
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`

### Test
- `xUnit`
- `Moq`
- `FluentAssertions`
- `coverlet.collector`

---

## 3) Mimari Yaklaşım

Projede **Light Clean Architecture** yaklaşımı kullandım.

### Katmanlar

#### `LotusCode.Api`
- Controller’lar
- Middleware
- Program.cs (DI, auth, swagger, pipeline)

#### `LotusCode.Application`
- DTO’lar
- Interface’ler
- Validatörler
- Exception tipleri
- Ortak response modelleri

#### `LotusCode.Domain`
- Entity’ler
- Enum’lar
- Status transition policy

#### `LotusCode.Infrastructure`
- EF Core DbContext
- Entity configuration’lar
- Service implementasyonları
- JWT servisleri
- Seed işlemleri

#### `LotusCode.Tests.Unit`
- Service, policy, validator testleri

### Sınırlar / Prensipler
- Controller içinde business logic yok
- Controller, DbContext’e direkt erişmiyor
- Business kurallar service/policy içinde
- Application, Infrastructure’a bağımlı değil
- Domain katmanı diğer katmanlardan bağımsız

---

## 4) Kurulum Rehberi

Repository: `https://github.com/Ilhanemreadak/fault-report-api`

## Gereksinimler
- .NET 8 SDK
- SQL Server (LocalDB / SQL Server Express / normal instance)
- (Opsiyonel) SSMS

## 1. Projeyi klonla

```bash
git clone https://github.com/Ilhanemreadak/fault-report-api.git
cd fault-report-api
```

## 2. Connection string ve JWT ayarlarını kontrol et

`src/LotusCode.Api/appsettings.json` içinde:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LotusCodeDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "LotusCode",
    "Audience": "LotusCode.Api",
    "SecretKey": "THIS_IS_A_DEVELOPMENT_ONLY_SUPER_SECRET_KEY_12345",
    "ExpirationMinutes": 60
  }
}
```

## 3. Migration’ı uygula

```bash
dotnet ef database update --project src/LotusCode.Infrastructure --startup-project src/LotusCode.Api
```

## 4. API’yi çalıştır

```bash
dotnet run --project src/LotusCode.Api
```

`launchSettings.json` profiline göre API şu adreslerde açılır:
- `http://localhost:5217`
- `https://localhost:7218`

Swagger:
- `https://localhost:7218/swagger`

## 5. Seed data

Uygulama açılırken seed otomatik çalışır.

Varsayılan kullanıcılar:
- **Admin** → `admin@lotus.local` / `Admin123!`
- **User** → `user@lotus.local` / `User123!`

Ek olarak idempotent şekilde örnek fault report kayıtları da eklenir.

> Seed lokasyonları: `Bursa/Nilüfer/Mahalle-{n}` formatında üretilir.

---

## 5) Kimlik Doğrulama ve Yetkilendirme

- Login endpointinden JWT alınır.
- Protected endpointlere `Authorization: Bearer {token}` header’ı ile gidilir.
- Tüm `fault-reports` endpointleri authorize ister.
- Status değiştirme endpointi sadece `Admin` rolüne açıktır.

---

## 6) API Response Standardı

Tüm endpointler standart bir wrapper döner:

```json
{
  "success": true,
  "data": {},
  "message": "...",
  "errors": []
}
```

Alanlar:
- `success`: işlem başarılı mı
- `data`: payload
- `message`: kullanıcıya/istemciye açıklama
- `errors`: hata detay listesi

---

## 7) İş Kuralları (Business Rules)

### 1) Duplicate Location Rule
- Aynı normalize edilmiş lokasyon için son 1 saat içinde yeni arıza kaydı açılamaz.
- Normalizasyon: `Trim()` + `ToLowerInvariant()`.

### 2) Ownership Rule
- `User` sadece kendi kayıtlarını görebilir/güncelleyebilir/silebilir.
- `Admin` tüm kayıtları yönetebilir.

### 3) Status Transition Rule
- Status sadece ayrı endpointten değişir.
- Geçişler merkezi policy ile doğrulanır.
- Sadece `Admin` status değiştirir.
- Geçersiz geçişlerde `422` döner.

### 4) Update Rule
- Normal update endpointi status değiştirmez.

---

## 8) Hata Yönetimi ve HTTP Kodları

Global middleware ile exception -> status code eşlemesi:

- `400 BadRequest` → ValidationException
- `401 Unauthorized` → UnauthorizedException
- `403 Forbidden` → ForbiddenException
- `404 NotFound` → NotFoundException
- `422 UnprocessableEntity` → BusinessRuleException, StatusTransitionException
- `500 InternalServerError` → beklenmeyen hatalar

Ayrıca global rate limit:
- Aynı IP için dakikada 10 istek
- Aşımda `429 Too Many Requests`

---

## 9) Endpoint Rehberi

Base URL (dev): `https://localhost:7218`

## Auth

### `POST /api/auth/login`
Kullanıcıyı doğrular, JWT üretir.

**Request**
```json
{
  "email": "admin@lotus.local",
  "password": "Admin123!"
}
```

**Response (200)**
`ApiResponse<LoginResponse>`

---

## Fault Reports (Authorize gerekli)

### `GET /api/fault-reports/{id}`
Tek kayıt detayını getirir.
- Admin: her kayda erişir
- User: sadece kendi kaydı

### `GET /api/fault-reports`
Listeleme + filtreleme + sıralama + sayfalama.

**Query parametreleri:**
- `status` (opsiyonel): `New, Reviewing, Assigned, InProgress, Completed, Cancelled, FalseAlarm`
- `priority` (opsiyonel): `Low, Medium, High`
- `location` (opsiyonel): contains filtre
- `page` (default 1)
- `pageSize` (default 10, max 100)
- `sortBy` (`createdAt` | `priority`)
- `sortDirection` (`asc` | `desc`)

### `POST /api/fault-reports`
Yeni arıza kaydı oluşturur.

**Request**
```json
{
  "title": "Trafo arızası",
  "description": "Bölgede elektrik kesintisi var.",
  "location": "Bursa/Nilüfer/Özlüce",
  "priority": "High"
}
```

**Not:** Status sistem tarafından `New` olarak atanır.

### `PUT /api/fault-reports/{id}`
Kayıt günceller (status hariç).

**Request**
```json
{
  "title": "Güncel başlık",
  "description": "Güncel açıklama",
  "location": "Bursa/Nilüfer/Beşevler",
  "priority": "Medium"
}
```

### `PATCH /api/fault-reports/{id}/status`  
Sadece admin status değiştirir.

**Request**
```json
{
  "status": "InProgress"
}
```

### `DELETE /api/fault-reports/{id}`
Kayıt siler.
- Admin: her kayıt
- User: sadece kendi kayıtları

---

## 10) Durum Geçiş Politikası

İzinli geçişler:

- `New` -> `Reviewing`, `Cancelled`
- `Reviewing` -> `Assigned`, `FalseAlarm`, `Cancelled`
- `Assigned` -> `InProgress`, `Cancelled`
- `InProgress` -> `Completed`, `Cancelled`
- `Completed`, `Cancelled`, `FalseAlarm` -> terminal (geçiş yok)

---

## 11) Kodlama Standartları

Projede takip ettiğim temel standartlar:

- Controller’lar ince tutuldu (iş kuralı içermez)
- Async/await uçtan uca kullanıldı
- DTO ile çalışma (entity doğrudan API dışına açılmadı)
- FluentValidation ile input validasyonu
- İş kuralları validator yerine service/policy katmanında
- `AsNoTracking()` read-only sorgularda kullanıldı
- Query filtre/sıralama/sayfalama SQL tarafına itildi
- Global exception middleware ile tek noktadan hata yönetimi
- XML yorumları ile Swagger dokümantasyonu iyileştirildi

---

## 12) Testler

Unit testleri çalıştırmak için:

```bash
dotnet test
```

Test odakları:
- `FaultReportService`
- `AuthService`
- `FaultReportStatusTransitionPolicy`
- Validator’lar

---

## 13) Kısa Kullanım Akışı

1. Login ol (`/api/auth/login`) ve token al
2. Token ile `fault-reports` endpointlerine istek at
3. User rolüyle kendi kayıtlarını yönet
4. Admin rolüyle status geçişlerini yönet

---
