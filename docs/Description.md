# SecurityRestApi — Specyfikacja API (OpenAPI 3.0.3)

## Opis

IdentityServer4-based identity provider z integracją federacji użytkowników Keycloak. Obsługuje granty ROPC, Client Credentials, Authorization Code PKCE oraz Token Exchange.

---

## Serwery

| URL | Środowisko |
|-----|------------|
| `https://localhost:5236` | Development (HTTPS) |
| `http://localhost:5000` | Development (HTTP) |

---

## Schematy bezpieczeństwa

### OAuth 2.0 — Password (ROPC)
```yaml
oauth2-ropc:
  type: oauth2
  flows:
    password:
      tokenUrl: /connect/token
      scopes:
        api: "SecurityRestApi"
        kg.internal: "InternalKgApi — dostęp wewnętrzny"
        openid: OpenID Connect
        profile: Profile claims (name, family_name, given_name)
        email: Email claim
```

### OAuth 2.0 — Client Credentials
```yaml
oauth2-client-credentials:
  type: oauth2
  flows:
    clientCredentials:
      tokenUrl: /connect/token
      scopes:
        kg.internal: "InternalKgApi — dostęp wewnętrzny"
```

### OAuth 2.0 — Authorization Code PKCE
```yaml
oauth2-pkce:
  type: oauth2
  flows:
    authorizationCode:
      authorizationUrl: /connect/authorize
      tokenUrl: /connect/token
      scopes:
        api: "SecurityRestApi"
        openid: OpenID Connect
        profile: Profile claims
        email: Email claim
```

### Bearer Token (JWT)
```yaml
bearerAuth:
  type: http
  scheme: bearer
  bearerFormat: JWT
```

---

## Endpointy — IdentityServer4 (OAuth 2.0 / OIDC)

### `POST /connect/token` — Żądanie tokena dostępu

Obsługiwane granty:
- **ROPC**: klient `ropc-client` (username + password)
- **Client Credentials**: klient `internalkg-client` (komunikacja serwis-serwis)
- **Token Exchange**: klient `securedweb-exchange-client` (wymiana tokena Keycloak na `kg.internal`)
- **Authorization Code (PKCE)**: klient `interactive` (aplikacja przeglądarkowa, wymaga przekierowania)

**Request body** (`application/x-www-form-urlencoded`):

| Parametr | Wymagany | Typ | Opis | Przykład |
|----------|----------|-----|------|----------|
| `grant_type` | tak | string | Rodzaj grantu | `password` |
| `username` | ROPC | string | Nazwa użytkownika | `jkowalski` |
| `password` | ROPC | string (password) | Hasło | `haslo123` |
| `client_id` | tak | string | Identyfikator klienta OAuth | `ropc-client` |
| `client_secret` | ROPC/CC | string | Sekret klienta | `secret` |

**Odpowiedź 200**:
```json
{
  "access_token": "eyJ...",
  "expires_in": 3600,
  "token_type": "Bearer",
  "scope": "openid profile email api"
}
```

---

### `GET /connect/authorize` — Rozpoczęcie flow PKCE

Przekierowuje użytkownika do strony logowania IdentityServer. Wymaga klienta `interactive` skonfigurowanego z PKCE.

**Parametry query**:

| Parametr | Wymagany | Typ | Opis |
|----------|----------|-----|------|
| `response_type` | tak | string | Wartość: `code` |
| `client_id` | tak | string | `interactive` |
| `redirect_uri` | tak | URI | `http://localhost:5236/signin-oidc` |
| `scope` | tak | string | `openid profile email api` |
| `code_challenge` | tak | string | PKCE challenge (S256) |
| `code_challenge_method` | tak | string | `S256` |

---

## Endpointy — Własne API

### `POST /auth/validate` — Walidacja danych logowania

Prosty endpoint walidujący dane logowania. Zwraca 200 OK dla poprawnych danych lub 401 Unauthorized w przeciwnym razie. Nie wystawia tokenów — do autoryzacji tokenowej użyj `/connect/token` z grantem ROPC.

**Request body**:
| Parametr | Wymagany | Typ | Opis |
|----------|----------|-----|------|
| `username` | tak | string | Nazwa użytkownika |
| `password` | tak | string (password) | Hasło |

**Status codes**: `200 OK`, `401 Unauthorized`, `500 Internal Server Error`

---

### `POST /auth/update-password` — Aktualizacja hasła użytkownika

Aktualizuje hasło istniejącego użytkownika w magazynie pamięciowym. Zwraca 204 No Content przy sukcesie, 400 Bad Request jeśli parametry są brakujące lub 404 Not Found jeśli użytkownik nie istnieje.

**Request body**:
| Parametr | Wymagany | Typ | Opis |
|----------|----------|-----|------|
| `username` | tak | string | Nazwa użytkownika |
| `password` | tak | string (password) | Nowe hasło |

**Status codes**: `204 No Content`, `400 Bad Request`, `404 Not Found`

---

### `GET /users/lookup` — Wyszukiwanie pojedynczego użytkownika

Wyszukuje pojedynczego użytkownika w magazynie pamięciowym według dostarczonego id, username lub email. Zwraca jeden wynik.

**Parametry query**:
| Parametr | Wymagany | Typ | Opis | Przykład |
|----------|----------|-----|------|----------|
| `id` | NIE | string | ID użytkownika | `1` |
| `username` | NIE | string | Nazwa użytkownika | `jkowalski` |
| `email` | NIE | string (email) | Adres email | `jan.kowalski@example.com` |

**Odpowiedź 200**:
```json
{
  "id": "1",
  "username": "jkowalski",
  "email": "jan.kowalski@example.com",
  "firstName": "Jan",
  "lastName": "Kowalski"
}
```

**Status codes**: `200 OK`, `404 Not Found`

---

### `GET /users` — Wyszukiwanie użytkowników przez federację Keycloak

Zapytuje podłączony serwer Keycloak o użytkowników używając dwóch trybów:
- **Wyszukiwanie pełnotekstowe**: gdy podano parametr `search`
- **Filtrowanie atrybutami**: gdy `search` jest puste, filtruje po username/email/firstName/lastName

**Parametry query**:

| Parametr | Wymagany | Typ | Domyślny | Opis |
|----------|----------|-----|----------|------|
| `search` | NIE | string | — | Term wyszukiwania pełnotekstowego |
| `username` | NIE | string | — | Filtr po username (gdy search puste) |
| `email` | NIE | string (email) | — | Filtr po emailu (gdy search puste) |
| `firstName` | NIE | string | — | Filtr po imieniu (gdy search puste) |
| `lastName` | NIE | string | — | Filtr po nazwisku (gdy search puste) |
| `first` | NIE | integer | 0 | Indeks pierwszego wyniku (paginacja) |
| `max` | NIE | integer | 20 | Maksymalna liczba wyników |

**Odpowiedź 200**: tablica obiektów `User[]`

---

### `GET /users/count` — Liczba użytkowników przez federację Keycloak

Zwraca całkowitą liczbę użytkowników pasujących do wyszukiwania pełnotekstowego z podłączonego serwera Keycloak.

**Parametry query**:
| Parametr | Wymagany | Typ | Opis |
|----------|----------|-----|------|
| `search` | NIE | string | Term wyszukiwania pełnotekstowego |

**Odpowiedź 200**:
```json
{
  "value": 1
}
```

---

## Schematy danych

### User
| Pole | Typ | Nullable | Przykład |
|------|-----|----------|----------|
| id | string | no | `"1"` |
| username | string | no | `"jkowalski"` |
| email | string | yes | `"jan.kowalski@example.com"` |
| firstName | string | yes | `"Jan"` |
| lastName | string | yes | `"Kowalski"` |

---

## Konfigurowani klienci OAuth 2.0

### ropc-client (Resource Owner Password)
- **Grant**: `password`
- **Sekret**: `secret`
- **Skopy**: `openid`, `profile`, `email`, `api`
- **Żywotność tokenu**: 3600s

### internalkg-client (Client Credentials — service-to-service)
- **Grant**: `client_credentials`
- **Sekret**: `kg-secret`
- **Skopy**: `kg.internal`
- **Żywotność tokenu**: 3600s

### securedweb-exchange-client (Token Exchange)
- **Grant**: `urn:ietf:params:oauth:grant-type:token-exchange`
- **Sekret**: `exchange-secret`
- **Skopy**: `kg.internal`
- **Żywotność tokenu**: 3600s

### interactive (Authorization Code PKCE — aplikacje przeglądarkowe)
- **Grant**: `authorization_code`
- **PKCE**: wymagane
- **Sekret**: brak
- **Redirect URI**: `http://localhost:5236/signin-oidc`
- **Skopy**: `openid`, `profile`, `email`, `api`
- **Żywotność tokenu**: 3600s

---

## Schemat bezpieczeństwa — globalne wymagania

Poniższe nagłówki bezpieczeństwa są domyślnie stosowane do wszystkich endpointów (chyba że nadpisane przez `security: []`):

```yaml
security:
  - bearerAuth: [kg.internal]
```

Oznacza to, że żądania muszą zawierać ważny token JWT z zakresem `kg.internal`.

---

## Pełna specyfikacja OpenAPI (YAML)

Pełna specyfikacja znajduje się w pliku [`SecurityRestApi.ApiSpec.yaml`](./SecurityRestApi.ApiSpec.yaml).
