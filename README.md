# Resto.pk Backend

**Resto.pk** is a multi-tenant restaurant website-builder SaaS backend built with ASP.NET Core and .NET 10. It provides the APIs and business logic required to manage restaurant workspaces, restaurants, staff memberships, digital menus, website templates, published restaurant websites, and subscription plans.

The backend follows Clean Architecture principles, uses Entity Framework Core with SQL Server for persistence, and implements authentication, tenant-aware authorization, permission-based access control, input validation, and centralized error handling.

**Repository:** [Masabali2/Resto.pk](https://github.com/Masabali2/Resto.pk)

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Architecture](#architecture)
- [Multi-Tenancy and Authorization](#multi-tenancy-and-authorization)
- [Authentication and Security](#authentication-and-security)
- [Website Publishing Workflow](#website-publishing-workflow)
- [Subscription Management](#subscription-management)
- [API Reference](#api-reference)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Database Migrations](#database-migrations)
- [API Response Format](#api-response-format)
- [Testing](#testing)
- [Frontend Integration](#frontend-integration)
- [Deployment Notes](#deployment-notes)
- [Project Status](#project-status)

## Overview

Resto.pk is designed to let restaurant businesses manage their digital presence from a shared SaaS platform.

A tenant represents a restaurant business workspace. A tenant can manage multiple restaurants, and users can have memberships in multiple tenants with different roles and permissions.

The backend separates business rules, application contracts, domain entities, database persistence, and HTTP API concerns. This structure supports maintainability and makes it easier to extend the platform as new features are introduced.

## Features

### Authentication and account security

- User registration and login.
- Email verification using one-time passwords (OTP).
- OTP resend handling with configurable expiration and rate limits.
- JWT-based authentication.
- Access and refresh tokens delivered through authentication cookies.
- Refresh-token rotation and session refresh.
- Logout and refresh-token revocation.
- Current-user endpoint.
- Forgot-password and reset-password endpoints.
- ASP.NET Core Identity for user and role management.

### Tenant and workspace management

- Create and manage tenant workspaces.
- Retrieve tenants associated with the current user.
- Support multiple active tenant memberships.
- Switch the active workspace through the `X-Tenant-Id` request header.
- Validate tenant membership on the server.
- Manage tenant details and status.
- Apply tenant-aware data access and authorization.

### Restaurant management

- Create and list restaurants within a tenant.
- Retrieve and update restaurant details.
- Change restaurant status.
- Soft-delete restaurants.
- Support multiple restaurants under one tenant.
- Manage restaurant assets and branding.

### Digital menu management

- Create and manage menus.
- Organize menu categories.
- Reorder categories.
- Create, retrieve, update, and manage menu items.
- Apply permission-based access policies.
- Support paginated menu and item queries.

### Restaurant website builder

- Browse available website templates.
- Create and manage restaurant websites.
- Associate websites with restaurants and templates.
- Save and update website draft configurations.
- Validate website configuration against the application's schema.
- Publish a saved draft.
- Unpublish a website while preserving its configuration.
- Preserve the published version while a new draft is being edited.
- Serve public website data by restaurant slug.
- Retrieve published website menus and branding assets through anonymous public endpoints.

### Staff and permissions

- Manage tenant staff memberships.
- Use role-based and permission-based authorization.
- Apply tenant membership checks to protected operations.
- Separate platform administration from tenant-level access.

### Subscription management

- Manage subscription plans and plan limits.
- Retrieve current subscription state and subscription history.
- Start and manage tenant subscriptions.
- Cancel subscriptions and manage subscription status.
- Configure auto-renewal state.
- Apply subscription entitlement rules to tenant operations.

Subscription management and payment processing are separate concerns. A payment gateway should not be considered integrated unless it has been implemented and configured separately.

### Platform administration

- Platform-level subscription-plan management.
- Plan activation and deactivation.
- Subscription-plan limit management.
- Platform oversight operations protected by platform-admin authorization.

### Infrastructure and cross-cutting concerns

- Entity Framework Core and SQL Server persistence.
- FluentValidation for request validation.
- Centralized exception-handling middleware.
- Consistent API response envelopes.
- Structured logging with Serilog.
- Email notification services and a durable email outbox.
- Background email delivery worker.
- Dependency injection for application services.
- Development-time database migrations and data seeding.

## Technology Stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core / .NET 10 |
| Language | C# |
| Architecture | Clean Architecture |
| ORM | Entity Framework Core |
| Database | Microsoft SQL Server |
| Authentication | ASP.NET Core Identity and JWT |
| Authorization | Role-based and permission-based policies |
| Validation | FluentValidation |
| API documentation | Swagger / OpenAPI |
| Logging | Serilog |
| Background processing | Hosted background worker; Hangfire packages are included |
| Email | SMTP-based email service |
| Testing | xUnit and project-specific test dependencies |
| Frontend | Angular in a separate repository |

## Architecture

The solution is organized into four main projects.

```text
Resto.pk/
├── RestaurantSaaS.slnx
├── Directory.Build.props
├── README.md
├── src/
│   ├── RestaurantSaaS.Api/
│   ├── RestaurantSaaS.Application/
│   ├── RestaurantSaaS.Domain/
│   └── RestaurantSaaS.Infrastructure/
└── tests/
    ├── RestaurantSaaS.Api.Tests/
    ├── RestaurantSaaS.Application.Tests/
    ├── RestaurantSaaS.Domain.Tests/
    └── RestaurantSaaS.Infrastructure.Tests/
```

### `RestaurantSaaS.Domain`

Contains the core business model, including:

- Domain entities and enums.
- Base and auditable entity abstractions.
- Tenant ownership and soft-deletion contracts.
- Value objects.
- Domain exceptions.
- Domain-level validation rules.

The domain layer is the foundation of the business model and should remain independent of HTTP and database implementation details.

### `RestaurantSaaS.Application`

Contains application contracts and business-operation abstractions, including:

- Service interfaces.
- Request and response DTOs.
- FluentValidation validators.
- Authorization permissions and policies.
- Common API response models.
- Application-level exceptions and interfaces.
- Website configuration schema.
- Entitlement-related contracts.

This layer defines what the application does without coupling its contracts to a particular HTTP endpoint or persistence implementation.

### `RestaurantSaaS.Infrastructure`

Contains implementation details, including:

- `ApplicationDbContext`.
- Entity Framework Core configurations and migrations.
- Identity persistence.
- Application services.
- Tenant resolution and tenant context.
- JWT and authentication-cookie services.
- Email delivery and notification implementations.
- File storage.
- Authorization handlers.
- Subscription and entitlement services.
- Development database seeding.

### `RestaurantSaaS.Api`

Provides the HTTP API and application entry point, including:

- Controllers and route definitions.
- Authentication and authorization setup.
- Dependency injection registration.
- FluentValidation integration.
- Tenant-resolution middleware.
- Exception-handling middleware.
- Swagger configuration.
- Serilog request logging.
- Application startup configuration.

### Dependency direction

The intended dependency structure is:

```text
API ───────────────► Application
 │                       ▲
 ▼                       │
Infrastructure ──────────┘

Application ───────► Domain
Infrastructure ────► Domain
```

The domain contains the core model, application defines the use-case contracts, infrastructure implements technical details, and the API exposes application functionality over HTTP.

## Multi-Tenancy and Authorization

Multi-tenancy is a central part of the Resto.pk backend.

### Tenant model

- A tenant represents a restaurant business workspace.
- A tenant can own multiple restaurants.
- A user can belong to multiple tenants.
- Memberships determine which tenant a user can access and which permissions they have.
- Tenant-specific data must be scoped to the resolved tenant context.

### Active tenant resolution

For authenticated tenant-scoped requests, the backend supports the `X-Tenant-Id` request header.

Example:

```http
X-Tenant-Id: <tenant-guid>
```

The header identifies a requested workspace; it does not grant access by itself. The backend validates that the authenticated user has an active membership in the selected tenant. When no tenant header is provided, the resolver can automatically select a tenant when the user has exactly one active membership.

Clients must not treat a tenant ID sent by the browser as proof of authorization.

### Authorization model

The backend combines:

1. **Authentication:** establishes the identity of the current user.
2. **Membership validation:** establishes whether the user belongs to the resolved tenant.
3. **Role and permission checks:** determine which operations the user can perform.
4. **Tenant-scoped data access:** restricts operations to the appropriate tenant.
5. **Subscription entitlement checks:** determine whether subscription-dependent operations are permitted.

Platform administration uses separate authorization policies from tenant-level operations.

## Authentication and Security

Authentication is implemented using ASP.NET Core Identity and JWT.

The authentication controller exposes registration, login, email verification, session refresh, logout, and password-recovery operations. Authentication cookies are set and cleared through a dedicated cookie service.

The JWT validation configuration checks issuer, audience, signing key, lifetime, and the expected signing algorithm.

Security-related implementation includes:

- Email-confirmation requirements for sign-in.
- Password policy enforcement.
- OTP expiration and attempt limits.
- Refresh-token session handling.
- Server-side tenant-membership validation.
- Permission-based authorization policies.
- Centralized exception handling.
- Configuration of secrets outside committed source code.

**Production security requirements:** use HTTPS, keep JWT and SMTP secrets in a secure configuration provider, configure cookie settings and CORS for the deployed frontend, and never commit real credentials.

## Website Publishing Workflow

The website builder separates editing from the public version of a website.

1. The tenant creates a website and selects an available template.
2. The editor saves website configuration as a draft.
3. The backend validates the draft configuration.
4. Publishing makes the saved version available through public-site endpoints.
5. Subsequent draft edits do not immediately replace the currently published version.
6. Unpublishing removes public availability while retaining the saved configuration.

Public endpoints resolve a published website using its slug. Anonymous visitors should receive only published public content, not private drafts or tenant-management data.

## Subscription Management

Subscription APIs allow tenants to inspect subscription state and history, start a subscription, and manage supported subscription settings.

Subscription-related access should be enforced by the backend, rather than relying on frontend route guards or disabled buttons.

The intended trial and entitlement design is:

- A tenant selects a subscription plan.
- An eligible tenant can receive a time-limited trial.
- Trial eligibility must be checked against subscription history.
- Operational access depends on subscription entitlement, not merely on the tenant's status.
- Subscription-management operations must remain accessible when required to resolve an expired subscription.
- Plan changes that require payment must not activate a new paid plan without a supported payment flow.

A live payment gateway and payment reconciliation should be documented as available only after those integrations are implemented and verified.

## API Reference

The API uses versioned and non-versioned route groups as defined by the existing controllers.

Swagger/OpenAPI is enabled in the Development environment.

### Authentication

Base route: `/api/auth`

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Register an account |
| POST | `/api/auth/login` | Authenticate and set authentication cookies |
| POST | `/api/auth/verify-otp` | Verify email OTP |
| POST | `/api/auth/resend-otp` | Request another verification OTP |
| GET | `/api/auth/me` | Get the current user |
| POST | `/api/auth/refresh` | Refresh the session |
| POST | `/api/auth/logout` | End the session |
| POST | `/api/auth/ForgotPassword` | Start password recovery |
| POST | `/api/auth/ResetPassword` | Reset a password |

### Tenants

Base route: `/api/tenants`

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/tenants` | Create a tenant |
| GET | `/api/tenants/mine` | List the current user's tenant memberships |
| GET | `/api/tenants/{tenantId}` | Retrieve a tenant |
| PUT | `/api/tenants/{tenantId}` | Update a tenant |

### Restaurants

Base route: `/api/restaurants`

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/restaurants` | Create a restaurant |
| GET | `/api/restaurants` | List restaurants |
| GET | `/api/restaurants/{restaurantId}` | Get a restaurant |
| PUT | `/api/restaurants/{restaurantId}` | Update a restaurant |
| PATCH | `/api/restaurants/{restaurantId}/status` | Change restaurant status |
| DELETE | `/api/restaurants/{restaurantId}` | Soft-delete a restaurant |

List endpoints support pagination where implemented.

### Menus, categories, and items

The menu API uses the `/api/v1` route prefix.

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/v1/restaurants/{restaurantId}/menus` | Create a menu |
| GET | `/api/v1/restaurants/{restaurantId}/menus` | List restaurant menus |
| GET | `/api/v1/menus/{menuId}` | Retrieve menu details |
| PUT | `/api/v1/menus/{menuId}` | Update a menu |
| PATCH | `/api/v1/menus/{menuId}/status` | Change menu status |
| DELETE | `/api/v1/menus/{menuId}` | Archive a menu |
| POST | `/api/v1/menus/{menuId}/categories` | Create a category |
| GET | `/api/v1/menus/{menuId}/categories` | List menu categories |
| PATCH | `/api/v1/menus/{menuId}/categories/order` | Reorder categories |
| GET | `/api/v1/menu-categories/{categoryId}` | Retrieve a category |
| PUT | `/api/v1/menu-categories/{categoryId}` | Update a category |
| POST | `/api/v1/menu-categories/{categoryId}/items` | Create a menu item |
| GET | `/api/v1/menu-categories/{categoryId}/items` | List category items |
| GET | `/api/v1/menus/{menuId}/items` | List menu items |
| GET | `/api/v1/menu-items/{itemId}` | Retrieve a menu item |
| PUT | `/api/v1/menu-items/{itemId}` | Update a menu item |

Additional category and item status-management endpoints are available in the corresponding controllers.

### Websites and templates

Website management uses `/api/v1/websites`.

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v1/websites/templates` | List available templates |
| POST | `/api/v1/websites` | Create a website |
| GET | `/api/v1/websites` | List websites |
| GET | `/api/v1/websites/{id}` | Retrieve a website |
| PUT | `/api/v1/websites/{id}` | Update website details |
| PUT | `/api/v1/websites/{id}/draft` | Save a draft configuration |
| POST | `/api/v1/websites/{id}/publish` | Publish the saved draft |
| POST | `/api/v1/websites/{id}/unpublish` | Unpublish the website |

The separate template controller also exposes template-related operations. Consult Swagger for its complete route and request details.

### Public restaurant websites

Base route: `/api/v1/public/sites`

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v1/public/sites/{slug}` | Retrieve published website data |
| GET | `/api/v1/public/sites/{slug}/menu` | Retrieve public menu data |
| GET | `/api/v1/public/sites/{slug}/branding` | Retrieve public branding assets |

These routes are designed for anonymous visitors and return public website information rather than tenant-management functionality.

### Subscriptions and plans

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/subscriptions/me` | Retrieve current subscription state |
| GET | `/api/subscriptions/tenants/{tenantId}` | Retrieve a tenant subscription |
| GET | `/api/subscriptions/tenants/{tenantId}/history` | Retrieve subscription history |
| POST | `/api/subscriptions/me/start` | Start a subscription |
| POST | `/api/subscriptions/{subscriptionId}/change-plan` | Request a plan change |
| POST | `/api/subscriptions/{subscriptionId}/cancel` | Cancel a subscription |
| PATCH | `/api/subscriptions/{subscriptionId}/status` | Update subscription status |
| PUT | `/api/subscriptions/{subscriptionId}/auto-renew` | Update auto-renewal settings |

Platform subscription-plan management uses `/api/platform/subscription-plans` and is restricted to platform administrators. It includes plan listing, retrieval, creation, limit updates, and activation/deactivation.

**API note:** The tables summarize routes declared by the current controllers; authorization requirements, request DTOs, status codes, and exact response schemas should be confirmed through the running Swagger document.

## Prerequisites

Install the following before running the backend:

- .NET 10 SDK.
- Microsoft SQL Server, SQL Server Express, or another supported SQL Server instance.
- SQL Server Management Studio or an equivalent database client, if needed.
- Git.
- An SMTP account or SMTP relay for email verification and notifications.

Check the installed .NET SDK:

```bash
dotnet --list-sdks
```

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/Masabali2/Resto.pk.git
cd Resto.pk
```

### 2. Configure the database

Update the `DefaultConnection` connection string using User Secrets, environment variables, or a secure configuration provider.

For local development, a SQL Server connection string can look like:

```text
Server=YOUR_SQL_SERVER;Database=RestaurantSaaS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Replace `YOUR_SQL_SERVER` with the name of your SQL Server instance.

Do not rely on another developer's local server name. Configure your own instance.

### 3. Configure JWT settings

The API requires a `Jwt` configuration section. Supply at least:

- `Issuer`
- `Audience`
- `Secret`
- `AccessTokenExpirationMinutes`
- `RefreshTokenExpirationDays`

The signing secret must be at least 32 characters long. Use a strong, unique secret and do not commit it to Git.

For local development, User Secrets can be configured from the API project directory:

```bash
cd src/RestaurantSaaS.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:Issuer" "Resto.pk"
dotnet user-secrets set "Jwt:Audience" "Resto.pk.Client"
dotnet user-secrets set "Jwt:Secret" "REPLACE_WITH_A_STRONG_SECRET_AT_LEAST_32_CHARACTERS"
dotnet user-secrets set "Jwt:AccessTokenExpirationMinutes" "30"
dotnet user-secrets set "Jwt:RefreshTokenExpirationDays" "7"
```

Replace the example secret before running the application.

### 4. Configure SMTP email settings

Email verification and notification delivery require valid SMTP configuration. Set the required values through User Secrets or environment variables according to the `EmailOptions` configuration class.

For example, the application uses an SMTP host, port, and sender name. SMTP authentication credentials should be stored securely and must not be committed to source control.

### 5. Restore dependencies

From the repository root:

```bash
dotnet restore RestaurantSaaS.slnx
```

### 6. Build the solution

```bash
dotnet build RestaurantSaaS.slnx
```

Resolve any build errors before proceeding.

### 7. Run the API

```bash
dotnet run --project src/RestaurantSaaS.Api/RestaurantSaaS.Api.csproj
```

In Development, the application is configured to apply pending Entity Framework Core migrations and seed initial data at startup.

Swagger UI is enabled in the Development environment. Open the local URL reported by the application and navigate to the Swagger UI route, typically `/swagger`.

The actual HTTP and HTTPS ports depend on the active launch profile.

## Configuration

The main configuration file is:

`src/RestaurantSaaS.Api/appsettings.json`

Configuration includes the database connection, Serilog levels, email verification options, SMTP defaults, and allowed hosts.

Sensitive values should be supplied through User Secrets during local development and a secure configuration mechanism in production.

Common configuration areas include:

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection |
| `Jwt:Issuer` | Expected JWT issuer |
| `Jwt:Audience` | Expected JWT audience |
| `Jwt:Secret` | JWT signing secret |
| `Jwt:AccessTokenExpirationMinutes` | Access-token lifetime |
| `Jwt:RefreshTokenExpirationDays` | Refresh-token lifetime |
| `Email` | SMTP and sender configuration |
| `EmailVerification` | OTP expiration and resend limits |
| `Serilog` | Logging levels and output configuration |

Environment variables can override configuration using double underscores, for example:

```text
ConnectionStrings__DefaultConnection
Jwt__Issuer
Jwt__Audience
Jwt__Secret
Email__SmtpHost
```

Use the property names defined by the corresponding options classes for any additional email credentials.

## Database Migrations

Database schema changes are managed with Entity Framework Core migrations in the Infrastructure project.

From the repository root, inspect available migrations:

```bash
dotnet ef migrations list --project src/RestaurantSaaS.Infrastructure/RestaurantSaaS.Infrastructure.csproj --startup-project src/RestaurantSaaS.Api/RestaurantSaaS.Api.csproj
```

Apply migrations explicitly when needed:

```bash
dotnet ef database update --project src/RestaurantSaaS.Infrastructure/RestaurantSaaS.Infrastructure.csproj --startup-project src/RestaurantSaaS.Api/RestaurantSaaS.Api.csproj
```

If the `dotnet-ef` command is not available, install a version compatible with the project's Entity Framework Core version.

**Important:** Development startup automatically applies migrations in the current API startup implementation. Production database migrations should be reviewed and applied through a controlled deployment process rather than relying on application startup.

## API Response Format

The application uses a shared `ApiResponse<T>` response model for many API operations.

A successful response generally follows this structure:

```json
{
  "success": true,
  "data": {},
  "errorCode": null,
  "message": null,
  "errors": null,
  "traceId": null
}
```

An error response generally follows this structure:

```json
{
  "success": false,
  "data": null,
  "errorCode": "VALIDATION_ERROR",
  "message": "The request could not be processed.",
  "errors": [],
  "traceId": "request-trace-id"
}
```

These are illustrative examples. Actual messages, error codes, and HTTP status codes depend on the endpoint and the error-handling implementation. Some endpoints, such as logout, can return a different response shape or status.

## Testing

The solution contains separate test projects for the API, application, domain, and infrastructure layers.

Run all tests from the repository root:

```bash
dotnet test RestaurantSaaS.slnx
```

Run a specific test project:

```bash
dotnet test tests/RestaurantSaaS.Api.Tests/RestaurantSaaS.Api.Tests.csproj
dotnet test tests/RestaurantSaaS.Application.Tests/RestaurantSaaS.Application.Tests.csproj
dotnet test tests/RestaurantSaaS.Domain.Tests/RestaurantSaaS.Domain.Tests.csproj
dotnet test tests/RestaurantSaaS.Infrastructure.Tests/RestaurantSaaS.Infrastructure.Tests.csproj
```

Important areas for continued automated coverage include:

- Authentication and refresh-token behavior.
- Tenant membership and cross-tenant isolation.
- Permission enforcement.
- Subscription trial eligibility and expiry.
- Subscription-management access after expiry.
- Website draft and publication behavior.
- Validation and exception handling.

The existence of test projects does not imply that every feature or scenario has been tested successfully. Run the tests against the current checkout before treating the build or test suite as verified.

## Frontend Integration

The Angular frontend is maintained separately:

[https://github.com/Masabali2/Resto.pkfrontend](https://github.com/Masabali2/Resto.pkfrontend)

When integrating the frontend:

- Use the backend's existing DTOs and `ApiResponse<T>` contract.
- Send credentials with requests that rely on authentication cookies.
- Use the active tenant's `X-Tenant-Id` header for tenant-scoped requests when required.
- Handle `401 Unauthorized`, `403 Forbidden`, validation errors, and subscription-entitlement errors.
- Treat frontend guards as a user-experience feature, not as the security boundary.
- Configure production CORS and cookie settings for the actual deployed frontend origin.
- Use the public-site endpoints for visitor-facing restaurant websites rather than management APIs.

## Deployment Notes

Before deploying to a production environment:

1. Configure a production SQL Server connection string.
2. Store JWT signing and SMTP credentials in a secure secret store.
3. Configure HTTPS, CORS, authentication-cookie settings, and allowed hosts.
4. Apply database migrations through a controlled release process.
5. Verify that public endpoints expose only published website data.
6. Confirm tenant membership, permission, and subscription-entitlement enforcement across tenant-scoped services.
7. Configure logging, health monitoring, backups, and operational alerting.
8. Verify email delivery and background-worker behavior.
9. Run automated tests and smoke-test critical API workflows.
10. Confirm the subscription and payment behavior matches the enabled product features.

Swagger is enabled by the current application startup only in Development. Production API documentation exposure should be an explicit deployment decision.

## Project Status

Resto.pk is an actively developed SaaS backend. Its current codebase includes authentication, tenant and restaurant management, menu operations, website-template and publication APIs, subscription-related operations, and supporting infrastructure.

The project should not be considered production-ready solely because these components exist. Before a production release, verify the latest code, complete tenant and entitlement authorization audits, test cross-tenant isolation, review subscription lifecycle edge cases, and confirm operational configuration.

## License

Add the project's chosen license here before distributing or publishing the backend for reuse.
