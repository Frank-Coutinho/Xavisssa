# Xavissa Backend Work Blueprint

This blueprint explains the backend from platform setup through a clerk finalizing a sale. It is based on the current ASP.NET Core backend, Entity Framework data model, services, controllers, JWT authentication, tenant/store scoping, licensing, catalog management, stock control, sales, refunds, auditing, and offline sync.

## 1. Backend Purpose

The backend is the authority for:

- Platform administration: tenants, support users, tenant admins, license plans, tenant licenses, device activations, and platform analytics.
- Tenant administration: stores, tenant users, store managers, clerks, printing settings, catalog, and tenant analytics.
- Store operation: product availability, variants, prices, stock levels, sales, payments, refunds, soft deletes, store analytics, and offline synchronization.
- Security: JWT authentication, platform/tenant/store roles, selected store context, query filtering, and audit logging.

The main request path is:

1. A system admin prepares the platform.
2. A tenant and license are created.
3. Tenant admin and store manager users are created.
4. Stores, clerks, products, variants, and stock are configured.
5. A clerk logs in, selects a store, loads sellable products, and posts a sale.
6. The backend validates license, store access, product activity, stock, totals, and payments.
7. The backend records the sale, sale items, payments, stock movements, audit logs, and sync metadata.

## 2. Runtime Architecture

Project entry point:

- `Backend/Xavissa.Backend/Program.cs`

Core layers:

- Controllers expose HTTP endpoints under `/api/...`.
- Services hold business logic:
  - `AuthService`
  - `SalesService`
  - `ProductService`
  - `LicenseService`
  - `SyncService`
  - `RoleService`
- `XavissaDbContext` maps tables, views, relationships, query filters, auditing, and sync metadata.
- PostgreSQL/Supabase is configured through the `Supabase` connection string.
- JWT bearer authentication is configured with `Jwt:Key`.
- Serilog writes console logs and daily files under `Logs/xavissa-.log`.

Important middleware sequence:

1. `UseAuthentication()`
2. `UseAuthorization()`
3. `RlsContextMiddleware`
4. `MapControllers()`

The middleware and DbContext work together so most queries are automatically constrained by the authenticated user's tenant and store scope.

## 3. Roles And Scope Model

The backend uses three role scopes:

- Platform roles:
  - `SYSTEM_ADMIN`
  - `SUPPORT`
  - `User`
- Tenant roles:
  - `TENANT_ADMIN`
  - `User`
- Store roles:
  - `STORE_MANAGER`
  - `CLERK`

Role hierarchy:

- System admin controls the platform.
- Support can operate across assigned tenants for support tasks.
- Tenant admin manages a tenant and can create store managers and clerks.
- Store manager manages assigned store operations and can create clerks.
- Clerk performs store sales and reads store-scoped catalog data.

Important note: `POST /api/UserManagement/create-system-admin` is intentionally disabled in the application. The first system admin must be seeded outside normal app flow or created by a maintenance/bootstrap process.

## 4. Authentication And Request Context

Authentication endpoints:

- `POST /api/Auth/login`
- `POST /api/Auth/select-store`

Login behavior:

1. User submits username and password.
2. `AuthService` loads user, platform role, tenant assignments, and store assignments.
3. Password is verified with `PasswordHasher`.
4. If valid, `LastLoginAt` is updated.
5. The response includes:
   - `UserId`
   - `Username`
   - `PlatformRole`
   - `ActingRole`
   - `SelectedTenantId`
   - `SelectedStoreId`
   - `AllowedTenants`
   - `AllowedStores`
   - `Token`, when a token can be issued for the current context

Store selection behavior:

1. User calls `POST /api/Auth/select-store`.
2. Backend verifies the user can access that store.
3. For assigned store users, the acting role becomes the store role.
4. For tenant admins, a store can be selected under their tenant.
5. A JWT is issued with selected tenant and selected store claims.

Request context values used by the backend:

- Current user id
- Platform role
- Acting role
- Allowed tenant ids
- Allowed store ids
- Selected tenant id
- Selected store id
- IP address

`TenantAccessService` provides the standard checks:

- `CanAccessTenant(tenantId)`
- `CanAccessStore(storeId)`
- `CanManageTenant(tenantId)`
- `CanManageStore(storeId)`
- `RequireSelectedStore()`

## 5. Database Guardrails

The DbContext applies:

- Global query filters for tenant/store isolation.
- Automatic audit fields for auditable entities.
- Automatic offline sync metadata for sync-enabled entities.
- Audit log creation for added, modified, and deleted entities.
- Store integrity validation.
- Role scope validation.

Sync metadata fields include:

- `SyncId`
- `SourceDeviceId`
- `ClientCreatedAt`
- `ClientUpdatedAt`
- `LastSyncedAt`

Audit log captures:

- Tenant and store when known
- User id
- Entity name
- Entity id
- Action type
- Old values JSON
- New values JSON
- Description
- IP address
- Created timestamp

## 6. Main Data Entities

Platform and identity:

- `User`
- `Role`
- `Tenant`
- `TenantUser`
- `UserStoreRole`
- `AuditLog`

Licensing:

- `LicensePlan`
- `License`
- `LicenseActivation`
- `LicensePayment`
- `LicenseUpgradeHistory`
- `LicenseValidationLog`
- `ActiveTenantLicenseView`
- `TenantLicenseUsageView`

Store and settings:

- `Store`
- `TenantPrintingSetting`
- `StorePrintingSetting`

Catalog and stock:

- `Category`
- `Product`
- `ProductStoreAssignment`
- `ProductVariant`
- `StockLevel`
- `StockMovement`
- `StockTransfer`
- `StockTransferItem`
- `StockAdjustment`
- `StockAdjustmentItem`
- `StoreSellableVariantView`

Sales:

- `Sale`
- `SaleItem`
- `SalePayment`
- `CashRegisterSession`
- `CashRegisterCashMovement`
- `CashRegisterSessionSummaryView`

## 7. System Admin Flow

The first system admin must already exist through seed data or a maintenance tool. Once authenticated as `SYSTEM_ADMIN`, the platform setup flow is:

1. Create license plans.
   - Endpoint: `POST /api/licensing/plans`
   - Creates available commercial or trial plans.
   - Plan limits can include max stores, users, devices, offline days, and feature flags.

2. Create a tenant.
   - Endpoint: `POST /api/Tenants`
   - Requires `SYSTEM_ADMIN`.
   - Creates the business/customer account.
   - Important fields: tenant name, code, active status.

3. Create a license for the tenant.
   - Endpoint: `POST /api/licensing/licenses`
   - Requires `SYSTEM_ADMIN`.
   - Generates a raw license key once and stores only its hash/prefix.
   - License status starts as `Active`.
   - Links tenant to a license plan.

4. Create support users when needed.
   - Endpoint: `POST /api/UserManagement/create-support`
   - Requires `SYSTEM_ADMIN`.
   - Creates a platform `SUPPORT` user.

5. Create tenant admin.
   - Endpoint: `POST /api/UserManagement/create-tenant-admin`
   - Requires `SYSTEM_ADMIN`.
   - Creates a normal platform user assigned as `TENANT_ADMIN` to the tenant.

6. Monitor platform analytics.
   - Endpoint: `GET /api/Analytics/platform`
   - Requires `SYSTEM_ADMIN`.
   - Returns total products, total stores, and total sales revenue.

## 8. Tenant Admin Flow

Tenant admin signs in and receives tenant-level permissions. They can manage stores, users, products, settings, and analytics for their tenant.

1. View tenant.
   - Endpoint: `GET /api/Tenants`
   - Non-platform users only see allowed tenants.

2. Update tenant profile.
   - Endpoint: `PUT /api/Tenants/{id}`
   - Requires tenant management permission.

3. Check license usage.
   - Endpoint: `GET /api/licensing/tenant/{tenantId}/usage`
   - Shows active plan, limits, stores used, users used, devices used, feature flags, validation dates, and grace period.

4. Create store.
   - Endpoint: `POST /api/Stores`
   - Requires tenant management permission.
   - Backend checks active license and store limit.
   - Store code is generated from the store name and made unique per tenant.

5. Update or deactivate store.
   - Endpoint: `PUT /api/Stores/{id}`
   - Endpoint: `DELETE /api/Stores/{id}`
   - Delete is a soft deactivation and also deactivates active store role assignments for that store.

6. Create store manager.
   - Endpoint: `POST /api/UserManagement/create-store-manager`
   - Requires support or tenant admin acting role.
   - Backend checks tenant user license limit before creation.

7. Create clerk.
   - Endpoint: `POST /api/UserManagement/create-clerk`
   - Requires support, tenant admin, or store manager acting role.
   - Store manager can create clerks only inside their managed store.

8. Assign or remove store roles.
   - Endpoint: `POST /api/UserStores`
   - Endpoint: `DELETE /api/UserStores?userId={id}&storeId={id}`
   - Tenant admin/support can assign store managers or clerks.
   - Store manager can assign clerks only.

9. Configure printing.
   - Endpoint: `GET /api/PrintingSettings/tenant/{tenantId}`
   - Endpoint: `PUT /api/PrintingSettings/tenant/{tenantId}`
   - Endpoint: `GET /api/PrintingSettings/store/{storeId}`
   - Endpoint: `PUT /api/PrintingSettings/store/{storeId}`

10. Review tenant analytics.
   - Endpoint: `GET /api/Analytics/tenant/{tenantId}`
   - Store managers are forbidden from tenant-level analytics.

## 9. Store Manager Flow

Store manager signs in with an assigned store. They can manage assigned store operations.

Typical responsibilities:

- View assigned stores.
- Create clerks for their selected store.
- Manage store catalog assignments and variants if allowed by tenant/store management checks.
- View store analytics.
- View sales.
- Refund sales or items.
- Soft-delete sales or sale items when allowed by management checks.

Common endpoints:

- `GET /api/Stores`
- `POST /api/UserManagement/create-clerk`
- `GET /api/Product/sellable`
- `GET /api/Sales`
- `GET /api/Sales/summary`
- `POST /api/Sales/{id}/refund`
- `POST /api/Sales/{saleId}/items/{saleItemId}/refund`
- `POST /api/Sales/{id}/soft-delete`
- `POST /api/Sales/{saleId}/items/{saleItemId}/soft-delete`
- `GET /api/Analytics/store`

## 10. Catalog Setup Flow

Before clerks can sell, the tenant/store must have sellable variants with stock.

1. Create or update categories.
   - `GET /api/Product/categories`
   - `POST /api/Product/categories`
   - `PUT /api/Product/categories/{id}`
   - `DELETE /api/Product/categories/{id}`
   - Delete deactivates a category.
   - Category names must be unique per tenant.

2. Create base product.
   - `POST /api/Product`
   - Requires tenant management.
   - Fields include name, description, category/category id, brand, active status.
   - Product code is generated.

3. Assign product to store.
   - `POST /api/Product/{id}/stores`
   - Creates or reactivates a `ProductStoreAssignment`.
   - The assignment links product, tenant, and store.

4. Create product variant for the store assignment.
   - `POST /api/Product/{id}/variants`
   - Variant needs price greater than zero.
   - Stock quantity cannot be negative.
   - Barcode can be supplied or generated.
   - SKU can be supplied or generated.
   - Backend creates a `ProductVariant` and upserts `StockLevel`.

5. Update variant or stock quantity.
   - `PUT /api/Product/variants/{variantId}`
   - Updates SKU, label, barcode, price, active status, and stock level.

6. Generate barcode.
   - `POST /api/Product/variants/{variantId}/generate-barcode`
   - `GET /api/Product/variants/{variantId}/barcode-image`

7. Load sellable products for POS.
   - `GET /api/Product/sellable?storeId={storeId}`
   - Returns one row per active sellable variant for the store, including product info, variant id, price, barcode, and stock.

Important catalog rules:

- Product belongs to a tenant.
- Product assignment belongs to one store.
- Variant belongs to a store assignment and tenant.
- Barcode must resolve to one exact variant.
- Inactive product, inactive assignment, or inactive variant cannot be sold.
- Products with sales history cannot be permanently deleted; they should be deactivated.

## 11. Clerk Login And POS Preparation

The clerk flow starts after a tenant admin or store manager has created the clerk and assigned a store role.

1. Clerk logs in.
   - `POST /api/Auth/login`
   - If the clerk has only one store, a token can be issued immediately.
   - If store selection is needed, the client calls `select-store`.

2. Clerk selects store.
   - `POST /api/Auth/select-store`
   - Backend verifies the clerk has active assignment to the store.
   - JWT includes selected tenant and selected store.

3. POS loads bootstrap/sync data.
   - `GET /api/Sync/bootstrap?includeCatalog=true`
   - Optional delta endpoints can refresh catalog, variants, stock, and sales.

4. POS loads sellable variants.
   - `GET /api/Product/sellable`
   - Requires selected store context.

5. POS can scan barcode.
   - `GET /api/Product/barcode/{barcode}`
   - Returns product and active variant data in the selected tenant/store context.

## 12. Sale Finalization Flow

The sale endpoint is:

- `POST /api/Sales`

Request shape:

- `tenantId`: optional; if omitted, backend uses selected tenant from token.
- `storeId`: present in DTO but backend uses selected store from token.
- `syncId`: optional; if empty, backend generates one.
- `sourceDeviceId`: optional device identifier.
- `clientCreatedAt` and `clientUpdatedAt`: optional offline timestamps.
- `discount`: optional decimal.
- `saleItems`: required list.
- `salePayments`: optional list; if empty, backend creates a default cash payment for the final total.

Sale item shape:

- `variantId`: required.
- `quantity`: required.
- Optional sync/device/client timestamp fields.

Sale payment shape:

- `paymentMethod`: defaults to `Cash`.
- `amount`: must be greater than zero to be included.
- `referenceNumber`: optional.
- `notes`: optional.
- Optional sync/device/client timestamp fields.

Backend preconditions:

1. Request must be authenticated.
2. JWT must contain a selected store.
3. Selected store must be accessible by current user.
4. Tenant must resolve from request tenant or selected tenant.
5. Tenant must have an active license.
6. Trial or demo license must not be expired.
7. Current user id claim must be valid.
8. Sale must contain at least one item.
9. Every item must reference a variant in the selected tenant and selected store.
10. Variant must be active.
11. Product must be active.
12. Product store assignment must be active.
13. Stock level must exist for each variant/store/tenant.
14. Stock quantity must be sufficient.

What `SalesService.CreateSaleAsync` does:

1. Loads all referenced variants for the selected tenant/store.
2. Includes product assignment, product, and stock levels.
3. For each sale item:
   - Validates variant exists.
   - Validates stock record exists.
   - Validates enough stock is available.
   - Decrements `StockLevel.QuantityOnHand`.
   - Updates stock level audit fields.
   - Creates a `SaleItem`.
   - Creates a negative `StockMovement` with movement type `Sale`.
4. Calculates gross total:
   - Sum of `UnitPrice * Quantity` for each sale item.
5. Applies discount:
   - Discount is capped at gross total.
   - Final total cannot be less than zero.
6. Normalizes payments:
   - Ignores payment rows with amount less than or equal to zero.
   - If no valid payments are supplied, creates one cash payment for the final total.
7. Calculates:
   - `PaymentStatus = Paid` when paid amount is at least final total.
   - `PaymentStatus = Partial` when paid amount is less than final total.
   - `ChangeGiven` when paid amount is greater than final total.
8. Creates `Sale`:
   - Tenant id
   - Store id
   - Sale date
   - Total amount
   - Discount
   - Payment status
   - Change given
   - Receipt number
   - Sale items
   - Payments
   - Sync metadata
9. Saves all changes in one DbContext save.
10. DbContext adds audit logs and sync timestamps.
11. Endpoint returns `201 Created` with the mapped sale response.

Receipt number format:

- `RC-{yyyyMMddHHmmss}-{shortGuid}`

Sale response includes:

- Sale id and sync id
- Tenant and store
- Sale date
- Total amount
- Discount
- Total paid
- Payment summary
- Payment status
- Change given
- Receipt number
- Refund flags
- Payment lines
- Sale item lines with product name, category, quantity, price, subtotal, refund fields, and refundable quantity

## 13. After-Sale Operations

View sales:

- `GET /api/Sales`
- `GET /api/Sales/{id}`
- `GET /api/Sales/summary?start={date}&end={date}`

Refund full sale:

- `POST /api/Sales/{id}/refund`
- Restores stock for every refundable item.
- Creates positive stock movements with movement type `Refund`.
- Marks items refunded when fully refunded.
- Marks sale refunded when every item is fully refunded.

Refund sale item:

- `POST /api/Sales/{saleId}/items/{saleItemId}/refund`
- Quantity must be greater than zero.
- Quantity cannot exceed remaining refundable quantity.
- Restores stock only for the refunded quantity.
- Updates item refund counters and sale refund status.

Soft-delete full sale:

- `POST /api/Sales/{id}/soft-delete`
- Requires store or tenant management permission.
- Restores stock for all remaining refundable quantities.
- Adds deletion audit log.
- Marks sale as refunded/voided.
- Sets payment status to `Voided`.

Soft-delete sale item:

- `POST /api/Sales/{saleId}/items/{saleItemId}/soft-delete`
- Requires store or tenant management permission.
- Restores stock for remaining refundable quantity.
- Marks item refunded.
- Recalculates sale totals, payment status, and change.
- Adds deletion audit log.

## 14. Offline Sync Flow

Sync endpoints:

- `GET /api/Sync/bootstrap?includeCatalog={bool}`
- `GET /api/Sync/sellable-variants?storeId={id}&updatedAfter={date}`
- `GET /api/Sync/stock-levels?storeId={id}&updatedAfter={date}`
- `GET /api/Sync/catalog?tenantId={id}&updatedAfter={date}`
- `GET /api/Sync/sales?storeId={id}&updatedAfter={date}`
- `POST /api/Sync/sales/upload`

Expected offline pattern:

1. Client activates or validates license.
2. Client logs in and selects store.
3. Client bootstraps store data.
4. Client stores catalog, stock, and sales locally.
5. Client posts sale uploads with stable `syncId` values.
6. Backend uses sync metadata to preserve offline device identity and timestamps.
7. Delta endpoints allow the client to fetch changes after the last sync point.

Important sync rule:

- `SyncId` is unique for sync-enabled entities and is generated if missing.

## 15. Licensing Flow

License endpoints:

- `GET /api/licensing/plans`
- `POST /api/licensing/plans`
- `GET /api/licensing/tenant/{tenantId}/usage`
- `POST /api/licensing/licenses`
- `GET /api/licensing/licenses/{licenseId}/activations`
- `POST /api/licensing/licenses/{licenseId}/suspend`
- `POST /api/licensing/licenses/{licenseId}/revoke`
- `POST /api/licensing/licenses/upgrade`
- `POST /api/licensing/activations/{activationId}/deactivate`
- `POST /api/licensing/activate`
- `POST /api/licensing/validate`

Where licensing is enforced:

- Creating stores checks active license and store limit.
- Creating tenant users checks active license and user limit.
- Creating sales checks active license and blocks expired trial/demo licenses.
- Device activation checks max devices.
- License validation extends grace period.

License statuses:

- `Active`
- `Suspended`
- `Revoked`

Trial/demo expiration:

- If `IsTrial` or `IsDemo` is true and `ExpiresAt <= DateTime.UtcNow`, restricted operations are blocked.

## 16. API Endpoint Map

Authentication:

- `POST /api/Auth/login`: login with username/password.
- `POST /api/Auth/select-store`: login and select store.
- `POST /api/Auth/register`: disabled; returns message to use role-specific user management endpoints.

User management:

- `POST /api/UserManagement`: disabled generic creation.
- `POST /api/UserManagement/create-system-admin`: disabled application-level system admin creation.
- `POST /api/UserManagement/create-support`: system admin creates support user.
- `POST /api/UserManagement/create-tenant-admin`: system admin creates tenant admin.
- `POST /api/UserManagement/create-store-manager`: support or tenant admin creates store manager.
- `POST /api/UserManagement/create-clerk`: support, tenant admin, or store manager creates clerk.
- `PUT /api/UserManagement/{id}`: update manageable user.
- `DELETE /api/UserManagement/{id}`: delete manageable user.

Users:

- `GET /api/Users/me`: current user.
- `GET /api/Users/all`: users visible to current manager/admin context.
- `GET /api/Users/{id}`: user by id.
- `PUT /api/Users/{id}`: update user.
- `DELETE /api/Users/{id}`: delete user.

User store assignments:

- `POST /api/UserStores`: assign user to store with store role.
- `DELETE /api/UserStores?userId={id}&storeId={id}`: remove store assignment.
- `GET /api/UserStores/{userId}`: list a user's store assignments.

Roles:

- `GET /api/Roles`: list all roles.
- `GET /api/Roles?scope={Platform|Tenant|Store}`: list roles by scope.

Tenants:

- `GET /api/Tenants`: list visible tenants.
- `POST /api/Tenants`: system admin creates tenant.
- `PUT /api/Tenants/{id}`: update tenant.

Stores:

- `GET /api/Stores`: list visible stores.
- `GET /api/Stores?tenantId={id}`: list stores for tenant.
- `POST /api/Stores`: create store, subject to license limits.
- `PUT /api/Stores/{id}`: update store.
- `DELETE /api/Stores/{id}`: deactivate store and assignments.

Products and categories:

- `GET /api/Product`: list products in selected context.
- `GET /api/Product/sellable`: list sellable variants for selected or supplied store.
- `GET /api/Product/catalog`: list tenant catalog.
- `GET /api/Product/{id}`: get product.
- `GET /api/Product/barcode/{barcode}`: get product by barcode.
- `GET /api/Product/categories`: list categories.
- `POST /api/Product/categories`: create category.
- `PUT /api/Product/categories/{id}`: update category.
- `DELETE /api/Product/categories/{id}`: deactivate category.
- `POST /api/Product`: create base product.
- `PUT /api/Product/{id}`: update product.
- `DELETE /api/Product/{id}`: delete product if no sales history.
- `GET /api/Product/{id}/stores`: list store assignments.
- `POST /api/Product/{id}/stores`: create/update store assignment.
- `DELETE /api/Product/{id}/stores/{storeId}`: deactivate product store assignment and variants.
- `GET /api/Product/{id}/variants`: list variants for store.
- `POST /api/Product/{id}/variants`: create variant and stock level.
- `PUT /api/Product/variants/{variantId}`: update variant and stock level.
- `DELETE /api/Product/variants/{variantId}`: deactivate/delete variant according to service logic.
- `POST /api/Product/variants/{variantId}/generate-barcode`: generate barcode.
- `GET /api/Product/variants/{variantId}/barcode-image`: get barcode PNG.

Sales:

- `GET /api/Sales`: list sales visible in current context.
- `GET /api/Sales/{id}`: get sale.
- `GET /api/Sales/summary`: sales summary by optional date range.
- `POST /api/Sales`: create/finalize sale.
- `POST /api/Sales/{id}/soft-delete`: void/soft-delete sale and restore stock.
- `POST /api/Sales/{saleId}/items/{saleItemId}/soft-delete`: soft-delete item and restore stock.
- `POST /api/Sales/{id}/refund`: refund full sale.
- `POST /api/Sales/{saleId}/items/{saleItemId}/refund`: refund specific sale item quantity.

Printing settings:

- `GET /api/PrintingSettings/tenant/{tenantId}`: get tenant receipt settings.
- `PUT /api/PrintingSettings/tenant/{tenantId}`: upsert tenant receipt settings.
- `GET /api/PrintingSettings/store/{storeId}`: get store receipt overrides.
- `PUT /api/PrintingSettings/store/{storeId}`: upsert store receipt overrides.

Licensing:

- `GET /api/licensing/plans`: list plans.
- `POST /api/licensing/plans`: create plan.
- `GET /api/licensing/tenant/{tenantId}/usage`: get active license limits and usage.
- `POST /api/licensing/licenses`: issue license.
- `GET /api/licensing/licenses/{licenseId}/activations`: list activations.
- `POST /api/licensing/licenses/{licenseId}/suspend`: suspend license.
- `POST /api/licensing/licenses/{licenseId}/revoke`: revoke license.
- `POST /api/licensing/licenses/upgrade`: change license plan.
- `POST /api/licensing/activations/{activationId}/deactivate`: deactivate device.
- `POST /api/licensing/activate`: activate license on device.
- `POST /api/licensing/validate`: validate license/device.

Analytics:

- `GET /api/Analytics/platform`: platform totals.
- `GET /api/Analytics/tenant/{tenantId}`: tenant/store breakdown.
- `GET /api/Analytics/store`: selected store sales analytics.

Audit logs:

- `GET /api/AuditLogs`: latest 500 logs visible in context.
- `GET /api/AuditLogs?tenantId={id}`: tenant logs.
- `GET /api/AuditLogs?storeId={id}`: store logs.

Sync:

- `GET /api/Sync/bootstrap`
- `GET /api/Sync/sellable-variants`
- `GET /api/Sync/stock-levels`
- `GET /api/Sync/catalog`
- `GET /api/Sync/sales`
- `POST /api/Sync/sales/upload`

Demo:

- `POST /api/demo/start`
- `POST /api/demo/events`
- `GET /api/demo/sessions`
- `GET /api/demo/sessions/{id}/events`
- `POST /api/demo/templates`

Health and test:

- `GET /health/connectivity`
- `GET /api/Test/ping`
- `GET /weatherforecast`

## 17. Standard End-To-End Scenario

This is the complete happy path from platform setup to finalized sale.

1. Seed first system admin.
2. System admin logs in.
3. System admin creates license plan.
4. System admin creates tenant.
5. System admin creates license for tenant.
6. System admin creates tenant admin.
7. Tenant admin logs in.
8. Tenant admin creates store.
9. Tenant admin creates store manager.
10. Tenant admin or store manager creates clerk.
11. Tenant admin assigns users to store roles when needed.
12. Tenant admin creates categories.
13. Tenant admin creates base products.
14. Tenant admin assigns products to store.
15. Tenant admin creates variants with price, barcode/SKU, and stock quantity.
16. Clerk logs in.
17. Clerk selects assigned store.
18. POS loads bootstrap and sellable variants.
19. Clerk scans barcode or selects products.
20. POS builds sale item list with variant ids and quantities.
21. POS collects payment lines.
22. POS posts `POST /api/Sales`.
23. Backend validates license, selected store, access, variant status, product status, assignment status, and stock.
24. Backend decrements stock.
25. Backend creates sale, sale items, payments, stock movements, sync metadata, and audit logs.
26. Backend returns created sale with receipt number and payment summary.
27. POS prints receipt using sale response plus tenant/store printing settings.
28. Later, manager can view analytics, refund items, void sale, or audit history.

## 18. Critical Business Rules

- A selected store is required for sale creation.
- Sales use selected store from JWT, not arbitrary request store id.
- A tenant must have an active license before sales are allowed.
- Expired trial/demo licenses block new sales.
- Store creation is blocked when license store limit is reached.
- Tenant user creation is blocked when license user limit is reached.
- Device activation is blocked when license device limit is reached.
- A sale must contain at least one item.
- Every sold item must reference an active product variant assigned to the selected store.
- Stock must exist and be sufficient before sale finalization.
- Sale discount is capped at the gross sale total.
- Payments default to cash for exact final total when none are supplied.
- Change is calculated when total paid is greater than final total.
- Refunds and soft deletes restore stock and create stock movements.
- Product deletion is blocked if the product has sales history.
- Query filters restrict tenant and store data based on JWT context.
- DbContext audit logging records data changes when a user context exists.

## 19. Implementation Gaps To Keep In Mind

- First system admin creation is not exposed through the application and needs a seed/bootstrap process.
- Cash register session entities exist, and sale payments support `CashRegisterSessionId`, but the current sale creation path does not require an open cash session.
- Stock transfers and stock adjustments entities exist, but no dedicated controller endpoints are currently exposed in the controller set reviewed here.
- Sale posting is transactional only through the single DbContext save; if higher concurrency becomes a concern, add explicit transaction/isolation and row-level stock conflict handling.
- The API has Swagger in development, but no versioned API prefix such as `/api/v1`.

## 20. Backend Verification Checklist

Before considering the backend flow production-ready, verify:

- Roles exist for all scopes: platform, tenant, and store.
- First system admin is seeded.
- JWT key and licensing signing secret are configured.
- Tenant creation works.
- License plan and license creation work.
- Tenant admin can create stores within license limits.
- Store manager and clerk creation obey role rules.
- Product, assignment, variant, stock, and barcode setup work.
- Clerk can only see assigned store data.
- Clerk cannot sell without selected store.
- Sale creation decrements stock exactly once.
- Sale creation rejects insufficient stock.
- Sale creation rejects inactive product, assignment, or variant.
- Sale creation rejects expired trial/demo licenses.
- Refund restores stock.
- Soft delete restores stock and marks sale/item correctly.
- Audit logs appear for creates, updates, deletes, refunds, and voids.
- Offline sync upload does not duplicate records when stable `syncId` values are reused.
