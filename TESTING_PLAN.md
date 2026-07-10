# Xavissa Production Readiness Testing Plan

## Purpose

This plan defines the testing required to decide whether Xavissa is production ready. It covers the ASP.NET Core backend, PostgreSQL database model, Avalonia desktop frontend, local SQLite offline mode, synchronization, licensing, stock control, sales, printing, security, deployment, and operational monitoring.

Production readiness means the system can safely support real store operations without losing sales, leaking tenant or store data, corrupting stock, breaking offline workflows, or failing silently.

## Current Testing Baseline

- The solution contains backend, frontend, shared, and database projects.
- The backend targets `net9.0`; the frontend targets `net8.0`.
- The app uses PostgreSQL remotely and SQLite locally.
- The app includes one current automated check: `Tests/OfflineSyncSchemaChecks.ps1`.
- No full unit, integration, end-to-end, load, security, or UI automation suites are currently present in the solution.

Because the existing automated coverage is small, this plan treats production readiness as a staged validation effort rather than a single final smoke test.

## Production Readiness Gates

The software should not be considered production ready until all of these gates pass:

1. Clean release builds for backend, frontend, shared, and database projects.
2. Database migrations apply successfully to a fresh PostgreSQL database and a copy of production-like data.
3. Local SQLite schema creation and upgrades work from clean install and existing install states.
4. Critical backend business rules are covered by automated tests.
5. Offline-first sales and sync paths are covered by automated tests and manual failover testing.
6. Role, tenant, and store isolation is verified with positive and negative tests.
7. Sales, stock, cash register, refunds, voids, and audit logs are validated end to end.
8. License activation, validation, offline grace limits, and device limits are validated.
9. Performance is acceptable under expected store and tenant load.
10. Backup, restore, logging, error reporting, and rollback procedures are tested.
11. A release candidate passes a manual acceptance test run on a real Windows device with printer hardware or approved printer simulation.

## Test Environments

### Local Developer Environment

Purpose:
- Fast feedback during development.

Requirements:
- Windows machine with .NET SDKs for the backend and frontend target frameworks.
- Local SQLite database created by the desktop app.
- Local or containerized PostgreSQL test database.
- Test configuration with fake license keys and test users.

### CI Environment

Purpose:
- Repeatable automated checks on every pull request or release branch.

Required jobs:
- `dotnet restore`
- `dotnet build Xavissa.sln -c Release`
- PowerShell schema checks: `Tests/OfflineSyncSchemaChecks.ps1`
- Unit tests
- Backend integration tests
- Local SQLite repository tests
- Static analysis and dependency vulnerability checks

### Staging Environment

Purpose:
- Production-like validation before release.

Requirements:
- PostgreSQL database isolated from production.
- Backend deployed with production-like settings.
- Windows desktop app connected to staging.
- Seed data for multiple tenants, stores, users, products, stock, license plans, and sales history.
- Test printers or PDF/virtual printer fallback.

### Pilot Environment

Purpose:
- Limited real-world validation before broad release.

Requirements:
- One or two real stores or controlled users.
- Daily backup verification.
- Active monitoring.
- Clear rollback procedure.

## Automated Test Suites To Add

### 1. Unit Tests

Recommended project structure:

- `Tests/Xavissa.Backend.UnitTests`
- `Tests/Xavissa.Frontend.UnitTests`
- `Tests/Xavissa.Database.UnitTests`

Recommended framework:
- xUnit or NUnit.
- FluentAssertions for readable assertions.
- NSubstitute or Moq for mocks.

Backend unit coverage:
- `AuthService`: password validation, disabled users, role selection, store selection, JWT response shape.
- `JwtService`: required claims, expiration, tenant and store claims.
- `TenantAccessService`: tenant access, store access, management permissions.
- `SalesService`: sale totals, discounts, payments, stock validation, refunds, voids, soft deletes.
- `ProductService`: product CRUD rules, categories, store assignments, variants, barcode generation.
- `StockAdjustmentService`: draft, approve, apply, cancel transitions.
- `StockTransferService`: create, approve, ship, receive, cancel transitions.
- `CashRegisterService`: open, close, cash movements, current session, summary.
- `LicenseService` and `LicenseKeyService`: activation, validation, expiration, plan limits, device limits.
- `SyncService`: idempotent sale upload, cursor behavior, conflict handling.

Frontend unit coverage:
- `LoginViewModel`: online login, offline login, invalid credentials, store choices, license activation.
- `HomeViewModel`: add to cart, barcode scanning, quantity changes, totals, discount handling, finalize sale.
- `HistoryViewModel`: pagination, filtering, CSV export, refund and delete workflows.
- `ManagementViewModel`: user/store/product/category/variant state changes.
- `ConfigViewModel`: printer settings, language, license usage display.
- `BackendHealthService`: offline, online, timeout, degraded responses.
- `LicenseCacheService`: signature validation, offline grace period, limited mode.
- `BarcodeScannerInputService`: scanner terminator, manual input, duplicate scans.

Database unit coverage:
- Entity validation defaults.
- Audit metadata generation.
- Offline sync metadata generation.
- Tenant/store scoped model conventions.

### 2. Backend Integration Tests

Recommended project:
- `Tests/Xavissa.Backend.IntegrationTests`

Recommended tools:
- `Microsoft.AspNetCore.Mvc.Testing`
- Testcontainers for PostgreSQL, or a disposable PostgreSQL test database.
- Real EF Core migrations.

Coverage:
- App starts with test configuration.
- `/health/connectivity` reports database status.
- Authentication endpoints return correct status codes and claims.
- Protected endpoints reject unauthenticated requests.
- Role-protected endpoints reject unauthorized roles.
- Tenant users cannot read or mutate another tenant's data.
- Store users cannot read or mutate another store's data.
- Migrations apply cleanly from zero.
- Critical endpoints return expected response contracts.

High-priority endpoint flows:
- Login and select store.
- Create tenant, store, tenant admin, store manager, clerk.
- Create product, assign to store, create variant, generate barcode.
- Open cash register, create sale, close register.
- Refund full sale and partial item.
- Void sale.
- Create stock adjustment and apply it.
- Create stock transfer and move it through the full lifecycle.
- Activate and validate a license.
- Bootstrap sync, pull deltas, upload offline sale batch.
- Resolve sync conflicts.

### 3. SQLite Offline Repository Tests

Recommended project:
- `Tests/Xavissa.Frontend.OfflineTests`

Use temporary SQLite files, not the developer's real local database.

Coverage:
- New local database creates all required tables.
- Existing local database upgrades without data loss.
- `CurrentSchemaVersion` changes are enforced.
- Offline sale is saved with `SyncId`, `SourceDeviceId`, timestamps, items, and payments.
- Offline sale reduces local stock.
- Unsynced sales are returned in correct order.
- Successful sync marks sale as synced and maps online IDs.
- Failed sync marks sale as failed but does not delete it.
- Product, category, store assignment, variant, and stock deltas are applied idempotently.
- Sync cursors are persisted and read correctly.
- Duplicate `SyncId` data is handled safely.

### 4. End-To-End Workflow Tests

Recommended approach:
- Start backend against staging or disposable PostgreSQL.
- Start the desktop app connected to that backend.
- Use manual scripts initially; add Avalonia UI automation where practical.

Critical E2E scenarios:

1. First platform setup
   - Seed or create system admin.
   - Create license plan.
   - Create tenant and license.
   - Create tenant admin, store manager, and clerk.

2. Store setup
   - Tenant admin creates store.
   - Store manager creates category, product, store assignment, variant, barcode.
   - Stock is added through adjustment.

3. Normal sale
   - Clerk logs in.
   - Clerk selects store.
   - Products load.
   - Item is added by click and barcode.
   - Sale is finalized with payment.
   - Receipt prints or PDF fallback is produced.
   - Stock decreases.
   - Sale appears in history and analytics.
   - Audit logs exist.

4. Offline sale and reconnect
   - Clerk logs in online once.
   - Backend is made unavailable.
   - Clerk logs in offline.
   - Clerk completes multiple sales.
   - Backend comes back online.
   - Sync uploads sales once.
   - Stock and sales match between SQLite and PostgreSQL.
   - No duplicate sale is created when sync is retried.

5. Refund and void
   - Full sale refund restores expected stock and marks sale correctly.
   - Partial item refund restores only refunded quantity.
   - Void workflow respects role and business restrictions.

6. Multi-tenant isolation
   - Tenant A user cannot see Tenant B stores, users, products, sales, stock, settings, audit logs, or analytics.

7. Multi-store isolation
   - Store A clerk cannot sell or view Store B stock or sales unless assigned.

8. License enforcement
   - Expired, suspended, revoked, over-device-limit, and offline-grace-expired license cases block or limit access as intended.

### 5. Manual Acceptance Tests

Manual testing is required because this is a desktop point-of-sale app with hardware-adjacent behavior.

Run these on a clean Windows machine:

- Install the app from the release artifact.
- Launch app without existing local database.
- Login online.
- Login offline after one successful online login.
- Switch between supported roles and confirm navigation visibility.
- Use the sales screen with keyboard, mouse, and barcode scanner input.
- Print receipt to physical printer.
- Print barcode label to physical label printer.
- Confirm PDF fallback when printer is missing.
- Confirm app recovery after forced close during a sale.
- Confirm app recovery after forced close during sync.
- Confirm app behavior after network drop, slow network, and backend 500 response.
- Confirm light/dark theme, language selection, and common screen sizes.

## Security Testing

### Authentication And Authorization

Test:
- Missing token.
- Invalid token.
- Expired token.
- Token signed with wrong key.
- Token missing selected tenant/store claims.
- Role escalation attempts.
- Store selection for unassigned store.
- Tenant access for unassigned tenant.

### Data Isolation

Test every major controller with cross-tenant and cross-store data:
- Products
- Variants
- Store assignments
- Stock levels
- Sales
- Cash register sessions
- Users
- Stores
- Analytics
- Audit logs
- Printing settings
- Sync endpoints

### Input Validation

Test:
- Negative prices and quantities.
- Discount greater than sale total.
- Empty product names and duplicate barcodes.
- Invalid enum values.
- Very large strings.
- Malformed JSON.
- SQL-like input strings.
- Invalid date ranges.
- Refund quantity greater than sold quantity.
- Stock transfer from and to same store.

### Secrets And Configuration

Verify:
- No production secrets in source control.
- JWT key is strong and environment-specific.
- Connection strings come from secure config.
- Logs do not expose passwords, license keys, JWTs, or full payment details.
- Swagger is disabled or protected in production.
- CORS policy is appropriate for deployment.

## Performance And Reliability Testing

### Backend Load Tests

Target workflows:
- Login and select store.
- Product catalog and sellable products.
- Create sale.
- Upload offline sales batch.
- Sales history and analytics.
- Stock delta sync.

Measure:
- P50, P95, and P99 response time.
- Error rate.
- Database CPU and locks.
- Memory growth.
- Connection pool usage.
- Slow queries.

Initial targets to refine with business expectations:
- Login under 1 second P95.
- Product list under 1 second P95 for realistic catalog size.
- Sale creation under 500 ms P95 under normal load.
- Sync batch of 100 offline sales completes without duplicates and within acceptable operational time.

### Desktop Reliability

Test:
- 8-hour cashier session without restart.
- 1,000 cart operations.
- 500 sales in local SQLite.
- Repeated sync every few minutes.
- Backend unavailable for a full shift, followed by reconnect.
- Large catalog startup and search performance.

### Data Durability

Test:
- App crash after local sale save but before sync.
- App crash during sync upload.
- Backend crash during sale creation.
- Duplicate sync retry.
- PostgreSQL backup and restore.
- SQLite backup or support export procedure.

## Reporting And Observability Tests

Verify:
- Backend logs include request errors, business rule failures, sync failures, and license validation failures.
- Logs include correlation data sufficient to trace a sale or sync batch.
- Audit logs are created for important entity changes.
- Failed sync records are visible to support users or admins.
- Health check detects database connectivity failure.
- Production error handling returns safe messages to clients.

## Test Data Matrix

Create staging seed data with:

- 2 tenants.
- 2 stores per tenant.
- System admin, support user, tenant admin, store manager, and clerk.
- Active, expired, suspended, revoked, and trial licenses.
- Products with and without variants.
- Active and inactive products.
- Duplicate-looking names with unique barcodes.
- Low stock, zero stock, and high stock products.
- Sales with cash, card, mixed payment, discount, refund, void, and soft delete states.
- Offline sales pending sync.
- Sync conflicts.

## Minimum Automated Coverage Before Production

These areas must have automated tests before production:

- Authentication and selected store token flow.
- Tenant and store authorization boundaries.
- Sale creation with stock and payment validation.
- Refund and void logic.
- Cash register open and close.
- Product, variant, barcode, and store assignment behavior.
- Stock adjustments and transfers.
- License activation and validation.
- SQLite schema creation and migration.
- Offline sale persistence and sync idempotency.
- Backend sync bootstrap, deltas, and upload.
- Database migrations and query filters.

## Release Candidate Test Run

For every release candidate:

1. Build Release backend and frontend.
2. Run all automated tests.
3. Run `Tests/OfflineSyncSchemaChecks.ps1`.
4. Apply migrations to fresh staging database.
5. Apply migrations to staging database with previous-version data.
6. Install desktop app on clean Windows machine.
7. Run the full manual acceptance checklist.
8. Run offline/reconnect scenario.
9. Run security role and tenant isolation smoke tests.
10. Run load test for sale creation and sync.
11. Verify logs, audit records, backups, and rollback procedure.
12. Sign off only if all blocker and critical issues are closed.

## Defect Severity

Blocker:
- Data loss, duplicate charges/sales, broken login, tenant data leak, stock corruption, app cannot start, migrations fail, or sync duplicates sales.

Critical:
- Major workflow unusable, incorrect totals, incorrect refund, incorrect license enforcement, unauthorized access, or printer flow unusable without fallback.

High:
- Important workflow requires workaround, intermittent sync failure, analytics materially wrong, degraded offline mode.

Medium:
- Non-critical UI issue, unclear validation, recoverable error handling issue.

Low:
- Cosmetic issue, minor copy issue, minor layout issue.

Production release requires:
- 0 blocker defects.
- 0 critical defects.
- High defects either fixed or explicitly accepted with mitigation.

## Suggested Implementation Order

1. Add test projects and CI build job.
2. Add backend unit tests for authentication, authorization, sales, stock, licensing, and sync.
3. Add disposable PostgreSQL backend integration tests.
4. Add temporary SQLite offline repository tests.
5. Add release-candidate smoke script for build, schema check, migration, and health check.
6. Add manual acceptance checklist as a tracked QA artifact.
7. Add load tests for sale creation, catalog reads, and sync upload.
8. Add staged pilot process and operational readiness checks.

## Production Readiness Decision

At the end of testing, produce a short release report containing:

- Build version and commit.
- Test environments used.
- Automated test results.
- Manual acceptance result.
- Load test result.
- Security test result.
- Known defects and accepted risks.
- Backup and rollback verification.
- Final recommendation: ready, ready with accepted risks, or not ready.

