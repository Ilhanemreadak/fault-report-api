# Fault Report Management API

This project is a **.NET 8 Web API** developed for an electricity distribution company scenario. The main goal is to manage fault reports in a secure, rule-driven, and maintainable way.

In this README, you will find:
- How to run the project
- How the architecture is designed
- Which libraries are used
- Coding standards and business rules
- Full endpoint descriptions

---

## 1) Project Overview

Main features provided by the API:

- JWT authentication
- Role-based authorization (`Admin`, `User`)
- Fault report CRUD operations
- Status transition policy
- Duplicate location prevention within 1 hour
- Global exception handling
- Standard API response wrapper (`ApiResponse<T>`)
- Swagger/OpenAPI documentation
- Serilog logging
- SQL Server + EF Core
- Seed data (admin/user + sample reports)
- Unit tests
- Rate limiting

---

## 2) Technologies and Libraries

### Platform
- `.NET 8`
- `ASP.NET Core Web API`

### Data Access
- `Entity Framework Core`
- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.EntityFrameworkCore.Tools`

### Authentication / Security
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `Microsoft.AspNetCore.Identity` (for password hashing)

### Validation
- `FluentValidation`
- `FluentValidation.AspNetCore`

### Documentation
- `Swashbuckle.AspNetCore` (Swagger UI)

### Logging
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`

### Testing
- `xUnit`
- `Moq`
- `FluentAssertions`
- `coverlet.collector`

---

## 3) Architecture Approach

The project follows a **Light Clean Architecture** approach.

### Layers

#### `LotusCode.Api`
- Controllers
- Middleware
- Program.cs (DI, auth, swagger, pipeline)

#### `LotusCode.Application`
- DTOs
- Interfaces
- Validators
- Exception types
- Common response models

#### `LotusCode.Domain`
- Entities
- Enums
- Status transition policy

#### `LotusCode.Infrastructure`
- EF Core DbContext
- Entity configurations
- Service implementations
- JWT services
- Seed operations

#### `LotusCode.Tests.Unit`
- Service, policy, validator tests

### Boundaries / Principles
- No business logic in controllers
- Controllers do not access DbContext directly
- Business rules are implemented in service/policy layers
- Application does not depend on Infrastructure
- Domain layer is independent from all other layers

---

## 4) Setup Guide

Repository: `https://github.com/Ilhanemreadak/fault-report-api`

## Requirements
- .NET 8 SDK
- SQL Server (LocalDB / SQL Server Express / standard instance)
- (Optional) SSMS

## 1. Clone the project

```bash
git clone https://github.com/Ilhanemreadak/fault-report-api.git
cd fault-report-api
```

## 2. Check connection string and JWT settings

In `src/LotusCode.Api/appsettings.json`:

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

> Note: `SecretKey` is for development only. In production, use secure secret management.

## 3. Apply migrations

```bash
dotnet ef database update --project src/LotusCode.Infrastructure --startup-project src/LotusCode.Api
```

## 4. Run the API

```bash
dotnet run --project src/LotusCode.Api
```

According to `launchSettings.json`, API runs on:
- `http://localhost:5217`
- `https://localhost:7218`

Swagger:
- `https://localhost:7218/swagger`

## 5. Seed data

Seed runs automatically on startup.

Default users:
- **Admin** → `admin@lotus.local` / `Admin123!`
- **User** → `user@lotus.local` / `User123!`

Additionally, sample fault reports are inserted idempotently.

> Seed locations are generated in the `Bursa/Nilüfer/Mahalle-{n}` format.

---

## 5) Authentication and Authorization

- Get JWT token from the login endpoint.
- Call protected endpoints with `Authorization: Bearer {token}` header.
- All `fault-reports` endpoints require authorization.
- Status update endpoint is restricted to `Admin` role.

---

## 6) API Response Standard

All endpoints return a standard wrapper:

```json
{
  "success": true,
  "data": {},
  "message": "...",
  "errors": []
}
```

Fields:
- `success`: operation status
- `data`: payload
- `message`: client-facing message
- `errors`: error detail list

---

## 7) Business Rules

### 1) Duplicate Location Rule
- A new fault report cannot be created for the same normalized location within the last 1 hour.
- Normalization: `Trim()` + `ToLowerInvariant()`.

### 2) Ownership Rule
- `User` can only view/update/delete their own reports.
- `Admin` can manage all reports.

### 3) Status Transition Rule
- Status can only be changed through the dedicated endpoint.
- Transitions are validated by a centralized policy.
- Only `Admin` can change status.
- Invalid transitions return `422`.

### 4) Update Rule
- Standard update endpoint does not allow status changes.

---

## 8) Error Handling and HTTP Status Codes

Global middleware maps exceptions to status codes:

- `400 BadRequest` → ValidationException
- `401 Unauthorized` → UnauthorizedException
- `403 Forbidden` → ForbiddenException
- `404 NotFound` → NotFoundException
- `422 UnprocessableEntity` → BusinessRuleException, StatusTransitionException
- `500 InternalServerError` → unexpected errors

Also global rate limiting:
- 10 requests per minute per IP
- Exceeding limit returns `429 Too Many Requests`

---

## 9) Endpoint Guide

Base URL (dev): `https://localhost:7218`

## Auth

### `POST /api/auth/login`
Authenticates user and returns JWT.

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

## Fault Reports (Authorization required)

### `GET /api/fault-reports/{id}`
Gets a single report detail.
- Admin: access any report
- User: access own report only

### `GET /api/fault-reports`
List endpoint with filtering + sorting + pagination.

**Query parameters:**
- `status` (optional): `New, Reviewing, Assigned, InProgress, Completed, Cancelled, FalseAlarm`
- `priority` (optional): `Low, Medium, High`
- `location` (optional): contains filter
- `page` (default 1)
- `pageSize` (default 10, max 100)
- `sortBy` (`createdAt` | `priority`)
- `sortDirection` (`asc` | `desc`)

### `POST /api/fault-reports`
Creates a new fault report.

**Request**
```json
{
  "title": "Transformer fault",
  "description": "There is a power outage in the area.",
  "location": "Bursa/Nilüfer/Özlüce",
  "priority": "High"
}
```

**Note:** Status is automatically assigned as `New`.

### `PUT /api/fault-reports/{id}`
Updates a report (except status).

**Request**
```json
{
  "title": "Updated title",
  "description": "Updated description",
  "location": "Bursa/Nilüfer/Beşevler",
  "priority": "Medium"
}
```

### `PATCH /api/fault-reports/{id}/status`
Only admin can update status.

**Request**
```json
{
  "status": "InProgress"
}
```

### `DELETE /api/fault-reports/{id}`
Deletes a report.
- Admin: any report
- User: own reports only

---

## 10) Status Transition Policy

Allowed transitions:

- `New` -> `Reviewing`, `Cancelled`
- `Reviewing` -> `Assigned`, `FalseAlarm`, `Cancelled`
- `Assigned` -> `InProgress`, `Cancelled`
- `InProgress` -> `Completed`, `Cancelled`
- `Completed`, `Cancelled`, `FalseAlarm` -> terminal (no outgoing transitions)

---

## 11) Coding Standards

Core standards followed in this project:

- Thin controllers (no business logic)
- End-to-end async/await usage
- DTO-based API contracts (no direct entity exposure)
- FluentValidation for input validation
- Business rules in service/policy layers, not validators
- `AsNoTracking()` for read-only queries
- Push filtering/sorting/pagination to SQL
- Centralized error handling via global exception middleware
- XML comments to improve Swagger docs

---

## 12) Tests

Run unit tests:

```bash
dotnet test
```

Test focus areas:
- `FaultReportService`
- `AuthService`
- `FaultReportStatusTransitionPolicy`
- Validators

---

## 13) Quick Usage Flow

1. Login (`/api/auth/login`) and get token
2. Use token for `fault-reports` endpoints
3. Manage own reports with `User` role
4. Manage status transitions with `Admin` role

---

## License:

This project is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International licence.

| Permitted Uses | Prohibited Uses | Terms |
| :--- | :--- | :--- |
| Copying and Sharing | Commercial Use (Making Money) | Attribution to the Author |
| Modifying the Code | Licence Change | Sharing with the Same Licence |

> [!IMPORTANT]
> None of this project's parts or the whole project may be used in a commercial product, service or paid education without prior written permission.

For commercial licence requests or collaboration: [LinkedIn](https://www.linkedin.com/in/ilhan-emre-adak/) | [Email](mailto:adak.ie@hotmail.com)
