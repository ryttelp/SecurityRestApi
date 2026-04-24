# InternalKgApi — Specyfikacja komponentu

## 1. Cel i kontekst

`InternalKgApi` to wewnętrzna usługa REST zapewniająca dostęp do zasobów grafu wiedzy (Knowledge Graph). Jest przeznaczona wyłącznie dla zaufanych klientów wewnętrznych (serwisy, workerzy, narzędzia backendowe) — nie dla użytkowników końcowych.

Uwierzytelnianie delegowane jest do istniejącego `SecurityRestApi`, który pełni rolę serwera autoryzacji (OAuth 2.0 / OpenID Connect, Duende IdentityServer). `InternalKgApi` nie wystawia własnych endpointów logowania — tylko weryfikuje tokeny wystawione przez `SecurityRestApi`.

---

## 2. Przepływ uwierzytelniania

Stosowany grant type: **Client Credentials** (`client_credentials`), zgodny z RFC 6749 §4.4.

```
┌─────────────┐   (1) POST /connect/token          ┌──────────────────┐
│   Klient    │ ─────────────────────────────────► │  SecurityRestApi │
│  (serwis)   │   client_id + client_secret         │  :5236           │
│             │ ◄───────────────────────────────── │                  │
└─────────────┘   (2) access_token (JWT, RS256)     └──────────────────┘
       │
       │  (3) GET /kg/... 
       │      Authorization: Bearer <access_token>
       ▼
┌─────────────────┐
│  InternalKgApi  │  (4) weryfikuje token lokalnie
│                 │      (JWKS z SecurityRestApi)
└─────────────────┘
```

### Kroki

| # | Kto | Co |
|---|-----|----|
| 1 | Klient | Wysyła `POST /connect/token` do SecurityRestApi z `client_id`, `client_secret`, `grant_type=client_credentials`, `scope=kg.internal` |
| 2 | SecurityRestApi | Zwraca podpisany JWT (RS256, TTL 3600 s) |
| 3 | Klient | Dodaje token jako nagłówek `Authorization: Bearer <token>` do każdego żądania do InternalKgApi |
| 4 | InternalKgApi | Waliduje podpis tokenu pobierając klucze publiczne z endpointu JWKS SecurityRestApi (`/.well-known/openid-configuration` → `jwks_uri`) |

Token jest cachowany po stronie klienta do czasu wygaśnięcia (`expires_in`). Klient powinien odświeżyć token z wyprzedzeniem (np. 60 s przed wygaśnięciem).

---

## 3. Konfiguracja SecurityRestApi — wymagane zmiany

Aby obsłużyć InternalKgApi, w `SecurityRestApi/IdentityServer/Config.cs` należy dodać:

### 3.1 Nowy API scope

```csharp
new ApiScope("kg.internal", "InternalKgApi — dostęp wewnętrzny")
```

### 3.2 Nowy klient (Client Credentials)

```csharp
new Client
{
    ClientId = "internalkg-client",
    ClientSecrets = { new Secret("<<SILNY_SECRET>>".Sha256()) },
    AllowedGrantTypes = GrantTypes.ClientCredentials,
    AllowedScopes = { "kg.internal" },
    AccessTokenLifetime = 3600,   // 1 godzina
    // Brak offline_access — grant CC nie używa refresh tokenów
}
```

> **Uwaga:** `<<SILNY_SECRET>>` należy zastąpić losowym ciągiem ≥ 32 znaków. W produkcji wartość powinna pochodzić z vault / zmiennych środowiskowych, nie z kodu.

### 3.3 Nowy zasób API (opcjonalnie, jeśli token ma zawierać audience)

```csharp
new ApiResource("internalkg", "InternalKgApi")
{
    Scopes = { "kg.internal" }
}
```

Dodanie `ApiResource` powoduje, że wystawiony token będzie zawierał claim `aud: internalkg`, co umożliwia weryfikację audience po stronie InternalKgApi.

---

## 4. Konfiguracja InternalKgApi

### 4.1 Weryfikacja tokenu (Program.cs / Startup)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://security-rest-api:5236";  // URL SecurityRestApi
        options.Audience = "internalkg";                      // musi pasować do ApiResource
        options.RequireHttpsMetadata = false;                 // tylko development
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("KgInternal", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "kg.internal"));
});
```

### 4.2 Zabezpieczenie endpointów

```csharp
app.UseAuthentication();
app.UseAuthorization();

// Przykład — każdy endpoint wymaga polityki "KgInternal"
app.MapGet("/kg/{id}", ...)
   .RequireAuthorization("KgInternal");
```

---

## 5. Przykładowe żądania

### Pobranie tokenu

```http
POST http://security-rest-api:5236/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=internalkg-client
&client_secret=<<SILNY_SECRET>>
&scope=kg.internal
```

**Odpowiedź:**

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "expires_in": 3600,
  "token_type": "Bearer",
  "scope": "kg.internal"
}
```

### Wywołanie InternalKgApi

```http
GET http://internal-kg-api:PORT/kg/entity/42
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

---

## 6. Obsługa błędów uwierzytelniania

| Sytuacja | HTTP Status | Opis |
|----------|-------------|------|
| Brak nagłówka `Authorization` | `401 Unauthorized` | `WWW-Authenticate: Bearer` |
| Token wygasły | `401 Unauthorized` | claim `exp` w przeszłości |
| Nieprawidłowy podpis tokenu | `401 Unauthorized` | klucz JWKS nie pasuje |
| Brak wymaganego scope | `403 Forbidden` | token ważny, ale brak `kg.internal` |
| Nieprawidłowe `audience` | `401 Unauthorized` | token nie wystawiony dla tej usługi |

---

## 7. Wymagania bezpieczeństwa

- `client_secret` przechowywany wyłącznie w vault / zmiennych środowiskowych; nigdy w repozytorium.
- SecurityRestApi w produkcji musi używać certyfikatu TLS (nie `AddDeveloperSigningCredential`).
- `RequireHttpsMetadata = false` dozwolone **tylko** w środowisku developerskim lub w izolowanej sieci wewnętrznej (np. k8s namespace bez zewnętrznego ruchu).
- InternalKgApi nie powinien być dostępny z zewnątrz sieci wewnętrznej (brak Ingress / firewall rule).
- JWKS jest pobierany przy starcie i cachowany — InternalKgApi automatycznie odświeża klucze przy rotacji (zachowanie domyślne `AddJwtBearer`).
- `AccessTokenLifetime` = 3600 s to rozsądny kompromis; przy wyższych wymaganiach bezpieczeństwa rozważ skrócenie do 600 s.

---

## 8. Środowiska

| Środowisko | SecurityRestApi URL | `RequireHttpsMetadata` | Signing credential |
|------------|---------------------|------------------------|--------------------|
| Development | `http://localhost:5236` | `false` | `AddDeveloperSigningCredential` |
| Staging / Prod | `https://security.internal` | `true` | certyfikat X.509 |

---

## 9. Diagram komponentów

```
┌─────────────────────────────────────────────────────────┐
│                     Sieć wewnętrzna                      │
│                                                         │
│  ┌──────────────┐    tokens    ┌──────────────────────┐ │
│  │  Klient      │ ──────────► │   SecurityRestApi    │ │
│  │  (serwis B)  │             │   Duende IdentityServer│ │
│  └──────┬───────┘             │   /connect/token     │ │
│         │                     │   /.well-known/...   │ │
│         │ Bearer token        └──────────┬───────────┘ │
│         ▼                                │ JWKS         │
│  ┌──────────────┐    weryfikuje ◄────────┘             │
│  │ InternalKgApi│                                       │
│  │  /kg/...     │                                       │
│  └──────────────┘                                       │
└─────────────────────────────────────────────────────────┘
```

---

## 10. Checklist implementacyjna

- [ ] Dodać scope `kg.internal` w `SecurityRestApi/IdentityServer/Config.cs`
- [ ] Dodać klienta `internalkg-client` (Client Credentials) w `Config.cs`
- [ ] Dodać `ApiResource "internalkg"` w `Config.cs`
- [ ] Wygenerować i bezpiecznie przechować `client_secret`
- [ ] Zaimplementować walidację JWT w `InternalKgApi` (`AddJwtBearer`)
- [ ] Zdefiniować politykę autoryzacji `KgInternal` (scope check)
- [ ] Zabezpieczyć wszystkie endpointy `InternalKgApi` polityką `KgInternal`
- [ ] Zaimplementować logikę cachowania tokenu po stronie klienta
- [ ] Upewnić się, że `InternalKgApi` nie jest osiągalny spoza sieci wewnętrznej
- [ ] W produkcji: zastąpić `AddDeveloperSigningCredential` certyfikatem X.509