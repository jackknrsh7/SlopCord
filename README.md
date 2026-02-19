<img width="800" height="600" alt="image" src="https://github.com/user-attachments/assets/3539a294-ee6c-4652-ad9c-f8ffa110e8bd" />

# SlopCord
Apptac makes a terrible vibe-coded self-hosted discord clone because ID requirements are bad.

---

## Architecture

SlopCord is a **.NET 10** microservices application built around a layered architecture. Each microservice is self-contained but shares common building blocks through a set of cross-cutting shared projects.

### Solution Structure

```
SlopCord.slnx
└── src/
    ├── Core/                          # Shared — no dependencies
    ├── Domain/                        # Shared — depends on Core
    ├── ApiShared/                     # Shared — ASP.NET Core startup abstractions
    │
    ├── Identity/
    │   ├── IdentityApi/               # ASP.NET Core Web API
    │   ├── IdentityService/           # Business logic
    │   └── IdentityPersistence/       # Data access
    │
    └── StaticPage/
        ├── StaticPageApi/             # ASP.NET Core Web API
        ├── StaticPageService/         # Business logic
        └── StaticPagePersistence/     # Data access
```

---

### Layer Responsibilities

#### `Core`
The foundation of the entire solution. Contains primitives, base types, constants, and utilities that have no dependency on any other project or framework. Every other project can safely depend on this layer.

#### `Domain`
Holds the domain model: entities, value objects, interfaces, and domain events shared across all microservices. Depends only on `Core`.

This is also where cross-cutting NuGet packages are pinned for the whole solution:
- **AutoMapper 14.0.0** — object-to-object mapping (last MIT-licensed version; 15.0+ is commercial)
- **MediatR 12.5.0** — mediator pattern for decoupled request/response and event handling (last MIT-licensed version; 13.0+ is commercial)

By placing these packages on `Domain`, all downstream projects receive them as transitive references automatically, ensuring a single version pin across the entire solution.

#### `ApiShared`
A shared class library that provides common ASP.NET Core startup infrastructure. Its centrepiece is `AbstractStartup`, which every API service's `Startup` class inherits from. See the [AbstractStartup](#abstractstartup) section below for details.

#### `{Service}Service`
The business logic layer for each microservice. Depends on `Domain`, giving it access to entities, interfaces, AutoMapper, and MediatR. MediatR handlers and AutoMapper profiles live here.

#### `{Service}Persistence`
The data access layer for each microservice. Depends on `{Service}Service`, allowing it to implement the repository and unit-of-work interfaces defined in the service layer.

#### `{Service}Api`
The HTTP entry point for each microservice. An ASP.NET Core Web API project that depends on both `{Service}Service` and `{Service}Persistence`, wiring them together through the DI container. Also depends on `ApiShared` for the shared startup infrastructure.

---

### Dependency Graph

```
Core
 └── Domain  ──────────────────────────────────────────┐
      └── {Service}Service                              │  (AutoMapper, MediatR)
           └── {Service}Persistence                    │
                └── {Service}Api ◄── ApiShared          │
                                                        │
                     (transitively receives Domain) ◄───┘
```

Simplified per-service chain:

```
Core → Domain → {Service}Service → {Service}Persistence → {Service}Api
                                                         ↑
                                              ApiShared ─┘
```

---

### Startup Pattern

Each service uses the classic `Startup.cs` pattern, bootstrapped via `Host.CreateDefaultBuilder` in `Program.cs`:

```csharp
// Program.cs (identical for every service)
Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>();
    })
    .Build()
    .Run();
```

> `WebApplicationBuilder.WebHost.UseStartup<T>()` is explicitly blocked by the ASP0010 analyzer in .NET 10. `Host.CreateDefaultBuilder` is the correct host for the `UseStartup` pattern.

#### `AbstractStartup`

`AbstractStartup` (in `ApiShared`) is the base class for every service's `Startup`. It establishes a consistent DI registration lifecycle and a shared middleware pipeline, while leaving service-specific concerns to the derived class via virtual/abstract hooks.

**Properties**

| Member | Description |
|---|---|
| `string ApiName` | Abstract. Each service declares its own name. |
| `IConfiguration Config` | Public getter, protected setter. Populated from the host's `IConfiguration` at construction. |

**DI registration lifecycle**

`ConfigureServices` (called by the host) orchestrates two override points, each individually wrapped in a try/catch:

```
ConfigureServices
 ├── try { AddBusiness(services) }  catch → HandleServiceError
 └── try { AddServices(services) }  catch → HandleServiceError
```

- **`AddBusiness`** *(protected virtual)* — intended for domain/business registrations: MediatR handlers, AutoMapper profiles, domain services.
- **`AddServices`** *(protected virtual)* — registers API infrastructure. The base implementation registers `AddOpenApi()` and `AddControllers()`, which all services inherit for free.

**Middleware pipeline**

`Configure` *(public virtual)* provides the default pipeline shared by all services:

```
UseHttpsRedirection
UseRouting
UseEndpoints
 ├── MapOpenApi()      (Development only)
 └── MapControllers()
```

**Error handling**

`HandleServiceError` *(public static)* is the last-chance logger invoked when either `AddBusiness` or `AddServices` throws during startup. At that point the DI container is not fully built, so logging infrastructure may be unavailable — it writes directly to `stderr` to ensure the failure is always surfaced.

**Concrete Startup (per service)**

A service's `Startup` only needs to declare `ApiName` and override `AddBusiness`/`AddServices` for anything service-specific. With no custom registrations yet, both derived classes are minimal:

```csharp
public class Startup(IConfiguration configuration) : AbstractStartup(configuration)
{
    public override string ApiName => "Identity";
}
```

---

### Adding a New Service

1. Create three projects following the naming convention: `{Name}Api` (webapi), `{Name}Service` (classlib), `{Name}Persistence` (classlib).
2. Set up project references: `Domain → {Name}Service → {Name}Persistence → {Name}Api`, plus `ApiShared → {Name}Api`.
3. Add all three to `SlopCord.slnx`.
4. Create a `Startup` inheriting `AbstractStartup` and a `Controllers/` directory with an empty controller.
5. Copy `Program.cs` verbatim from any existing service — it is identical for all.

---

### Configuration & Secrets

> **Never commit `appsettings.json` or any environment-specific config file to source control.** These files can contain database connection strings, JWT signing keys, API keys, and other credentials. Exposing them — even briefly — in a public or shared repository is a serious security risk that cannot be fully undone by deleting the file later, since git history retains every version ever committed.

Each API project contains two configuration files:

| File | Committed? | Purpose |
|---|---|---|
| `appsettings.json` | **No** — gitignored | Your real local/production config, including any secrets |
| `appsettings.example.json` | **Yes** | A safe template that documents every key the app expects, with placeholder values in place of real secrets |

**Setup for a new developer or deployment environment:**

1. Copy `appsettings.example.json` to `appsettings.json` in the same directory.
2. Replace every `YOUR_*_HERE` placeholder with the real value for that environment.
3. Never add `appsettings.json` to git — the `.gitignore` blocks it, but stay conscious of this.

The `.gitignore` in this repo enforces the pattern broadly:

```gitignore
appsettings*.json        # ignores appsettings.json, appsettings.Development.json, etc.
!appsettings.example.json  # explicitly allows the example template
```

If you need environment-specific overrides (e.g. `appsettings.Development.json`), those are also gitignored by the same rule. Use environment variables, a secrets manager (e.g. Azure Key Vault, AWS Secrets Manager), or the [.NET Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) tool for local development instead of committing environment files.

---

### NuGet Package Notes

| Package | Version | Reason for pin |
|---|---|---|
| AutoMapper | 14.0.0 | 15.0+ requires a commercial license (Lucky Penny Software, July 2025) |
| MediatR | 12.5.0 | 13.0+ requires a commercial license (Lucky Penny Software, July 2025) |
| Microsoft.AspNetCore.OpenApi | 10.0.3 | Pinned on `ApiShared`; flows transitively to all API projects |
