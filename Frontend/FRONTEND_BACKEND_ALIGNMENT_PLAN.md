# Xavissa Frontend Functionality Flow And Backend Alignment Plan

This document defines the target frontend behavior, screen design, data flow, and implementation changes required for the Avalonia frontend to match the current backend architecture.

The backend is now the source of truth for tenant scope, store scope, roles, licensing, catalog structure, sellable variants, stock, sales, refunds, cash register operations, audit logs, and offline sync. The frontend should behave as a role-aware, offline-capable POS and operations client, not as a simple local product/sale app.

## 1. Frontend Architecture Goal

The frontend should be organized around four layers:

1. Shell and session layer
   - Starts the local backend process.
   - Ensures the local SQLite workspace schema.
   - Performs license activation or demo startup.
   - Logs the user in online when possible.
   - Falls back to cached offline identity when the backend is unavailable.
   - Stores the bearer token through `IApiTokenProvider`.
   - Holds current user, tenant, store, and acting role in `IAuthService`.

2. Sync and local cache layer
   - Uses `ISyncService` as the only orchestration point for bootstrap, deltas, reconnect sync, post-sale sync, and store switch refresh.
   - Keeps local SQLite tables aligned with backend sync DTOs.
   - Uses cursors per tenant/store:
     - `catalog:{tenantId}`
     - `sellable:{storeId}`
     - `stock:{storeId}`
     - `sales:{storeId}`
   - Uploads local pending sales before pulling fresh server state.

3. Repository/API layer
   - Uses online repositories for authenticated backend operations.
   - Uses offline repositories for POS-critical reads and writes.
   - Routes all authenticated calls through the named `backend` `HttpClient`.
   - Treats `401` as expired/invalid session and `403` as a scope or role denial.

4. View model/UI layer
   - Keeps all screen logic role-aware.
   - Reads permissions from `IAuthService`, not from hard-coded user names or local-only role assumptions.
   - Shows only actions that the selected tenant/store context can perform.
   - Never asks the user to manually provide tenant/store ids except in admin contexts where a real selection is required.

## 2. Backend Concepts The UI Must Respect

The backend request context is built from JWT claims and selected scope:

- `UserId`
- `PlatformRole`
- `ActingRole`
- `SelectedTenantId`
- `SelectedStoreId`
- allowed tenant ids
- allowed store ids

The frontend must therefore treat login as a two-step scope flow:

1. Authenticate username/password with `POST /api/Auth/login`.
2. If the response does not include a store-scoped token and the user has selectable stores, call `POST /api/Auth/select-store`.

The UI must not assume all logged-in users are store-scoped. Tenant admins may work at tenant level without a selected store, then select a store for store-specific operations.

Backend role model:

- `SYSTEM_ADMIN`: platform administration only.
- `SUPPORT`: support tasks across assigned tenants.
- `TENANT_ADMIN`: tenant setup, stores, users, catalog, printing, analytics.
- `STORE_MANAGER`: assigned store operations, clerks, variants, stock, sales history.
- `CLERK`: POS sales and store-scoped reads.

The current frontend mainly covers `TENANT_ADMIN`, `STORE_MANAGER`, and `CLERK`. A complete alignment should add platform/support surfaces or deliberately block those roles with a clear "not available in desktop client" state.

## 3. Application Startup Flow

### Current target behavior

1. `App.axaml.cs` creates the DI host.
2. Register common services:
   - workspace selection
   - local SQLite factory
   - backend token handler
   - connectivity
   - printer/theme/localization
   - license client/cache
   - auth/session state
   - repositories
   - view models
3. Create `MainWindow` with `AppViewModel`.
4. In the background:
   - ensure the local SQLite schema
   - start the backend process on `http://localhost:5087`
5. `AppViewModel` shows `LoginViewModel`.

### Required changes

- Add a visible startup state before login when the backend is still starting. The user can still continue offline, but the UI should explain whether it is using online or cached mode.
- Replace fixed backend base URL configuration with a configuration object. The default can remain `http://localhost:5087`, but it should not be hard-coded inside DI setup.
- Add a backend readiness indicator to the login screen using `GET /health/connectivity`.
- Add a session-expired handler. When any repository receives `401`, clear token/session and route to login.

## 4. License And Workspace Flow

### Target flow

The login screen has three paths:

1. Real workspace with active local license
   - Validate signed cached license.
   - Open real local workspace.
   - Let user log in online or offline.

2. First activation
   - User enters license key.
   - Frontend sends device info to `POST /api/licensing/activate`.
   - Save returned signed license cache.
   - Switch to real workspace.
   - Ensure schema.
   - Prompt for normal login.

3. Demo
   - Reset demo workspace.
   - Ensure schema.
   - If online, call `POST /api/demo/start`.
   - Start local demo identity with tenant admin/store manager capabilities.

### Required changes

- Add explicit license status states:
  - `NotActivated`
  - `Activated`
  - `Expired`
  - `OfflineGrace`
  - `DeviceMismatch`
- Add license usage display for tenant admins using `GET /api/licensing/tenant/{tenantId}/usage`.
- Prevent real login when license cache is missing or invalid, unless the backend returns a valid online activation/validation result.
- Add a clear demo banner when using the demo workspace so demo data is never confused with real tenant data.

## 5. Authentication And Store Selection Flow

### Target flow

1. User enters username and password.
2. Frontend ensures local schema.
3. Frontend clears old token.
4. If backend is ready:
   - `POST /api/Auth/login`.
   - Cache returned identity and token if present.
   - If token is missing and a single allowed store exists, call `POST /api/Auth/select-store`.
   - If multiple stores exist, open a store picker before entering the shell.
   - Save the resulting identity for offline login.
5. If backend is not ready:
   - validate cached offline identity and password.
   - start offline session.
6. Shell opens with role-specific default view:
   - tenant admin: analytics or management
   - store manager: analytics or management
   - clerk: POS

### Required changes

- Add a real store-selection dialog before entering `MainViewModel` when multiple stores exist.
- When store changes inside the shell, call `POST /api/Auth/select-store` online to obtain a store-scoped token. The current `IAuthService.SetSelectedStore` changes local state only; backend RLS needs the selected store claim.
- Persist the selected store and token after successful store selection.
- Support tenant-level mode for tenant admins:
  - selected tenant set
  - selected store optional
  - POS and stock operations disabled until a store is selected
- Add UI handling for users with no allowed tenant/store:
  - show "no assigned workspace"
  - disable app navigation
  - allow logout only

## 6. Main Shell Design

### Layout

The shell should stay operational rather than marketing-like:

- Left navigation rail with stable width.
- Top context bar with:
  - current user
  - role label
  - tenant/store selector
  - online/offline status
  - sync status
  - time/date
- Main content area with current module.
- Global snackbar/notification host.

### Navigation by role

Tenant admin:

- Analytics
- Management
  - Stores
  - Users
  - Categories
  - Products
  - Product assignments
  - Variants by store
- History
- Settings
  - tenant printing defaults
  - store printing overrides
  - license usage
- Audit logs
- Sync conflicts

Store manager:

- Analytics
- POS
- History
- Store products/variants
- Stock operations
  - adjustments
  - transfers
- Cash register
- Settings
  - store printing overrides
- Sync conflicts

Clerk:

- POS
- History, if allowed by policy
- Cash register, if cash register mode is enabled
- Settings limited to local printer/preferences

System admin/support:

- Either add a platform admin shell or block with a clear unsupported-desktop-role screen.
- Platform admin shell should include tenants, license plans, issued licenses, support users, platform analytics, and audit logs.

### Required changes

- Add missing navigation entries for:
  - Cash Register
  - Stock Operations
  - Audit Logs
  - Sync Conflicts
  - Platform Admin, if desktop should support `SYSTEM_ADMIN`
- Add permission properties to `IAuthService` for each backend operation instead of broad booleans only.
- Ensure each navigation item has disabled/loading/empty states and is hidden only when the user should never access it.

## 7. POS Flow

### Target screen design

The POS screen should be optimized for speed:

- Product search and barcode entry at the top.
- Category filter and grid/list toggle.
- Sellable variant list, not base product list.
- Each sellable item shows:
  - product name
  - variant label
  - price
  - stock on hand
  - barcode/SKU
  - out-of-stock disabled state
- Cart panel pinned to the right.
- Payment area below cart:
  - discount
  - payment method
  - amount tendered
  - change
  - finalize
  - print receipt toggle or automatic print setting

### Target data flow

1. On entering POS, load local sellable variant snapshots.
2. If online and store-scoped, refresh:
   - `GET /api/Sync/bootstrap?includeCatalog=false`
   - or delta endpoints for sellable variants and stock.
3. Barcode scan:
   - first search local sellable snapshots.
   - if not found and online, call `GET /api/Product/barcode/{barcode}`.
   - cache result if it belongs to the selected store.
4. Add item to cart:
   - use variant id as the sellable identity.
   - prevent quantity above local stock unless backend policy allows negative stock.
5. Finalize sale:
   - create local `Sale`, `SaleItem`, and `SalePayment`.
   - include `SyncId`, `SourceDeviceId`, `ClientCreatedAt`, tenant id, store id.
   - if online, post `POST /api/Sales`.
   - if offline, mark unsynced.
   - print receipt from local sale result.
   - after online sale, call post-sale sync to refresh stock and sale state.

### Required changes

- Make the cart line identity variant-based. Current code still maps through `Product` and must continue eliminating base-product assumptions.
- Rename UI labels and models from "Products" to "Sellable variants" where stock and sale behavior is involved.
- Enforce selected store before allowing sale.
- Add cash register gate:
  - if store cash register mode is enabled, block sale finalization unless an open cash session exists for this device/store.
- Add better failure handling for online sale:
  - `400`: show validation message from backend.
  - `403`: show permission/scope issue.
  - stock conflict: save local result only if backend confirms conflict strategy; otherwise keep cart and prompt refresh.
- Add multi-payment UI if the backend should accept multiple `SalePayments`.

## 8. Cash Register Flow

### Target screen design

Cash Register should be a store-scoped module with four states:

1. Disabled
   - Store operational setting says cash register is disabled.
   - POS does not require open session.

2. No open session
   - Manager/clerk can open register.
   - Input: opening cash amount and notes.

3. Open session
   - Shows opened time, opening cash, expected cash, cash sales, cash in/out.
   - Actions:
     - cash in
     - cash out
     - close register

4. Closing
   - Input counted cash.
   - Shows expected cash and difference.
   - Submit close.

### Backend endpoints

- `GET /api/StoreOperationalSettings/store/{storeId}`
- `PUT /api/StoreOperationalSettings/store/{storeId}`
- `GET /api/CashRegister/current`
- `POST /api/CashRegister/open`
- `POST /api/CashRegister/close`
- `POST /api/CashRegister/cash-movements`
- `GET /api/CashRegister/sessions/{id}/summary`

### Required changes

- Add `ICashRegisterRepository`.
- Add `CashRegisterViewModel` and `CashRegisterView.axaml`.
- Add cash register local cache sync or a strict online-only policy. If online-only, the POS must explain why cash mode cannot continue offline.
- Store `SourceDeviceId` on open/close/movement requests.
- Add cash session status to the POS header.

## 9. Sales History, Refunds, And Soft Deletes

### Target screen design

History should be a transaction review workspace:

- Date filter:
  - today
  - yesterday
  - this week
  - custom range
- Tenant admin store filter.
- Search by receipt, payment reference, product/variant name, SKU/barcode.
- Paginated sale table.
- Detail drawer:
  - receipt number
  - store
  - clerk
  - items
  - payments
  - discount
  - refund/void status
  - audit metadata if available
- Manager actions:
  - full refund
  - item refund with quantity
  - void sale
  - void item
  - export CSV

### Backend endpoints

- `GET /api/Sales`
- `GET /api/Sales/{id}`
- `GET /api/Sales/summary`
- `POST /api/Sales/{id}/refund`
- `POST /api/Sales/{saleId}/items/{saleItemId}/refund`
- `POST /api/Sales/{id}/soft-delete`
- `POST /api/Sales/{saleId}/items/{saleItemId}/soft-delete`

### Required changes

- Use server-side date/store pagination when online instead of loading everything.
- Keep offline history paginated from SQLite.
- After refund/void, refresh sales and stock cursors.
- Add reason requirement validation before submitting refund/void.
- Show backend error details in the confirmation dialog if an action fails.

## 10. Tenant And Store Management Flow

### Tenant admin flow

1. View tenant analytics and license usage.
2. Manage stores:
   - list stores from `GET /api/Stores`
   - create store with `POST /api/Stores`
   - update store with `PUT /api/Stores/{id}`
   - deactivate store with `DELETE /api/Stores/{id}`
   - respect license max store limits
3. Manage users:
   - list visible users from `GET /api/Users/all`
   - create store manager with `POST /api/UserManagement/create-store-manager`
   - create clerk with `POST /api/UserManagement/create-clerk`
   - assign users to stores with `POST /api/UserStores`
   - remove assignment with `DELETE /api/UserStores`
4. Manage tenant printing defaults.
5. Manage catalog categories and base products.
6. Assign products to stores and create variants.

### Store manager flow

1. View assigned store analytics.
2. Manage clerks for assigned store.
3. Manage store-specific product variants.
4. Run stock operations.
5. View/refund/void sales where permitted.
6. Manage store printing overrides.

### Required changes

- Split the current Management module into clearer sub-workspaces internally, even if they remain tabs:
  - `TeamManagementViewModel`
  - `StoreManagementViewModel`
  - `CategoryManagementViewModel`
  - `CatalogManagementViewModel`
  - `VariantManagementViewModel`
- Keep the current tab UI if desired, but isolate API calls and validation per workflow.
- Make user creation role-specific. Do not call generic user creation endpoints.
- Show license limit errors from the backend when creating stores/users/devices.
- Require store assignment after creating a clerk/store manager when backend response does not automatically assign one.

## 11. Catalog, Assignment, Variant, And Barcode Flow

The backend separates product concepts:

- `Category`: tenant-level grouping.
- `Product`: tenant-level base catalog item.
- `ProductStoreAssignment`: product availability in a specific store.
- `ProductVariant`: store-specific sellable item.
- `StockLevel`: stock for a variant in a store.
- `StoreSellableVariantView`: read model for POS.

### Target frontend behavior

Tenant admin:

1. Create categories.
2. Create base products.
3. Assign products to stores.
4. Create or review store variants.

Store manager:

1. View assigned store products.
2. Create/update variants for assigned products.
3. Generate barcode for a variant.
4. Print labels.
5. Manage stock through adjustment/transfer modules, not by editing stock casually inside the product card unless this creates a backend stock adjustment.

### Required changes

- Ensure base product editing never directly mutates store stock.
- Any stock quantity change from a variant editor should either:
  - call variant update only when backend intentionally supports it, or
  - create a `StockAdjustment` workflow.
- Add barcode image preview using `GET /api/Product/variants/{variantId}/barcode-image`.
- Add bulk label printing for selected variants.
- Add explicit assignment state:
  - not assigned to store
  - assigned with no variants
  - assigned with active variants
  - inactive/removed

## 12. Stock Operations Flow

### Stock adjustments

Use when counted stock differs from system stock.

Flow:

1. Store manager opens Stock Adjustments.
2. Select store, if tenant admin.
3. Search variants.
4. Enter counted/new quantity and reason.
5. Create adjustment with `POST /api/StockAdjustments`.
6. Approve with `POST /api/StockAdjustments/{id}/approve`.
7. Apply with `POST /api/StockAdjustments/{id}/apply`.
8. Refresh stock cursor.

### Stock transfers

Use when moving stock between stores.

Flow:

1. Tenant admin or manager selects source and destination stores.
2. Add variants and requested quantities.
3. Create transfer with `POST /api/StockTransfers`.
4. Approve, ship, receive, or cancel through backend transition endpoints.
5. Refresh stock for affected stores.

### Required changes

- Add `IStockOperationsRepository`.
- Add `StockOperationsViewModel` and `StockOperationsView.axaml`.
- Add local models to repository mapping for `StockAdjustment*` and `StockTransfer*`.
- Add transition-aware UI:
  - Draft/Requested
  - Approved
  - Sent/Shipped
  - Received/Applied
  - Cancelled
- Do not allow direct destructive stock edits from POS.

## 13. Settings And Printing Flow

### Target behavior

Settings should separate local device preferences from backend-managed receipt settings.

Local device preferences:

- theme
- language
- receipt printer
- label printer
- local paper dimensions
- label layout defaults

Backend tenant/store printing:

- tenant default receipt header/footer/logo
- store overrides
- persisted via:
  - `GET /api/PrintingSettings/tenant/{tenantId}`
  - `PUT /api/PrintingSettings/tenant/{tenantId}`
  - `GET /api/PrintingSettings/store/{storeId}`
  - `PUT /api/PrintingSettings/store/{storeId}`

### Required changes

- Add a visible distinction between "This device" and "Business receipt settings".
- Load backend printing settings after login and store switch.
- Save backend printing settings only when user clicks Save, not every time a local preference changes.
- Keep local printer selection local; do not send OS printer names to backend unless explicitly required.

## 14. Analytics Flow

### Target behavior

Tenant admin:

- Load `GET /api/Analytics/tenant/{tenantId}`.
- Show tenant totals and store breakdown.
- Include store status and last sale date.

Store manager:

- Load `GET /api/Analytics/store`.
- Show selected store sales totals, average sale value, recent trend, top products/variants if backend adds them.

Offline:

- Fall back to local SQLite sales/stores/products, clearly marked as cached.

### Required changes

- Add date range filters if backend supports query parameters later.
- Mark charts as cached/offline when built from local database.
- Avoid showing tenant-level analytics to store managers unless backend explicitly allows it.

## 15. Audit Logs And Sync Conflicts

### Audit logs

Target users:

- system admin
- support
- tenant admin
- store manager for store-scoped logs, if allowed

Flow:

1. Open Audit Logs.
2. Filter by tenant/store/entity/action/user/date.
3. Load `GET /api/AuditLogs`.
4. Show old/new JSON in a readable diff drawer.

Required frontend changes:

- Add `IAuditLogRepository`.
- Add `AuditLogsViewModel` and `AuditLogsView.axaml`.
- Add role-aware navigation item.

### Sync conflicts

Flow:

1. Open Sync Conflicts.
2. Load unresolved conflicts from `GET /api/Sync/conflicts`.
3. Show local payload, server payload, conflict type, created date.
4. Resolve with the backend's resolution endpoint.
5. Refresh sync.

Required frontend changes:

- Add `ISyncConflictRepository`.
- Add `SyncConflictsViewModel` and `SyncConflictsView.axaml`.
- Link failed sale uploads to conflict details.
- Surface conflict badge in the shell.

## 16. Offline Sync Workflow

### Target sync order

On online login:

1. Save online identity and token.
2. Upload pending sales:
   - `POST /api/Sync/sales/upload`
3. Bootstrap current scope:
   - `GET /api/Sync/bootstrap?includeCatalog=true|false`
4. Pull sales delta:
   - `GET /api/Sync/sales`
5. Store cursors.
6. Notify view models that online data has been applied.

On reconnect:

1. Upload pending sales.
2. Pull store-scoped bootstrap/deltas.
3. Refresh POS, history, analytics, management.

On store switch:

1. Online: call `POST /api/Auth/select-store` and update token.
2. Clear store-scoped screen state.
3. Pull selected store sellable variants and stock.
4. Pull selected store sales.
5. Reload POS/history/analytics.

On sale:

1. Save local sale.
2. Online: post sale or upload batch.
3. Pull stock delta.
4. Pull sales delta.
5. Update cart/history.

### Required changes

- Ensure `SyncAfterReconnectAsync` uploads pending sales before pulling data.
- Ensure `SyncStoreScopedDataAsync` does not rely only on local selected store changes; update backend token first.
- Add sync result status:
  - last sync time
  - pending sale count
  - failed sale count
  - conflict count
- Add retry action for failed uploads.

## 17. API And DTO Alignment Changes

The frontend should converge on DTOs that mirror backend read/write contracts:

- `LoginResponse`
- `LoginTenantDto`
- `LoginStoreDto`
- `SaleCreateDto`
- `SaleItemDto`
- `SalePaymentDto`
- `SaleReadDto`
- `CategoryReadDto`
- `SaveCategoryDto`
- `ProductReadDto`
- `ProductCreateDto`
- `UpdateProductDto`
- `ProductStoreAssignmentDto`
- `ProductVariantReadDto`
- `SaveProductVariantDto`
- `StoreBootstrapSyncDto`
- `CatalogDeltaDto`
- `StoreSellableVariantsDeltaDto`
- `StockLevelsDeltaDto`
- `SalesDeltaDto`
- `SalesUploadBatchRequestDto`
- `CashRegister*Dto`
- `StockAdjustment*Dto`
- `StockTransfer*Dto`
- `SyncConflictDto`

Required changes:

- Move duplicated frontend DTOs toward a shared contract or a clearly named `Models/Dtos/Backend` namespace.
- Remove typo-prone filenames/interfaces over time:
  - `LocalBdContext.cs`
  - `IProcductRepositoryOnline.cs`
  - `IsaleRepository.cs`
  - `PrinterConvigViewModel.cs`
  - `AppView.axam.cs`
- Keep compatibility wrappers while refactoring so the app can be changed safely.

## 18. Error, Loading, And Empty States

Every module should use standard states:

- `Loading`: spinner/skeleton with current operation.
- `Empty`: no records for this scope.
- `Offline`: cached mode banner and disabled online-only actions.
- `Denied`: role or scope does not permit action.
- `InvalidScope`: user must select tenant/store first.
- `Failed`: backend/local error with retry.
- `SyncPending`: local changes waiting to upload.

Backend response handling:

- `400`: validation/business rule message.
- `401`: session expired; clear token and return to login.
- `403`: role/scope denial; keep session but disable action.
- `404`: stale local data or deleted entity; refresh relevant cache.
- `409`: sync/stock conflict; open conflict workflow.
- `5xx`: keep local state and offer retry.

## 19. Visual Design Direction

The app is an operational POS/admin tool. The design should be dense, predictable, and fast:

- Use compact cards only for repeated entities, not nested page sections.
- Prefer tables/lists for admin data.
- Prefer drawers/dialogs for editing selected records.
- Keep POS touch targets large enough for cashier use.
- Use stable dimensions for:
  - navigation rail
  - product tiles
  - cart rows
  - toolbar buttons
  - numeric inputs
- Use clear status badges:
  - Online/Offline
  - Synced/Pending/Failed
  - Active/Inactive
  - Open/Closed register
  - In stock/Low stock/Out of stock
- Avoid hidden destructive actions. Refunds, voids, deactivations, stock applications, and register closing need confirmation and reason fields.

## 20. Implementation Roadmap

### Phase 1: Session and scope correctness

- Add store picker before shell entry.
- Make store switching call `POST /api/Auth/select-store`.
- Add session-expired handling.
- Add tenant-level state for tenant admins.
- Add shell sync/status indicators.

### Phase 2: POS and sync hardening

- Complete variant-based cart behavior.
- Enforce selected store for sales.
- Upload pending sales before pull sync.
- Add post-sale stock/sales refresh.
- Add stock conflict handling.

### Phase 3: Management alignment

- Split Management view model responsibilities.
- Make user creation endpoint-specific.
- Tighten catalog/product/store assignment flow.
- Add barcode preview and bulk label printing.
- Add license usage and backend limit messages.

### Phase 4: Operations modules

- Add Cash Register repository/view/viewmodel.
- Add Store Operational Settings handling.
- Add Stock Operations repository/view/viewmodel.
- Gate POS by cash register mode when required.

### Phase 5: Admin observability

- Add Audit Logs module.
- Add Sync Conflicts module.
- Add platform/support shell or explicit unsupported-role screen.

### Phase 6: Polish and verification

- Standardize loading/error/empty states.
- Rename typo-prone files/interfaces.
- Add focused tests around:
  - login/store selection
  - offline login
  - sale creation DTO mapping
  - sync cursor updates
  - refund/void flow
  - stock/cash register request mapping

## 21. Definition Of Done

The frontend matches the backend workflow when:

- A user can log in online, select tenant/store scope, and receive a token valid for backend RLS.
- The same user can log in offline after one successful online login.
- Tenant admin, store manager, and clerk see different navigation and actions based on backend roles.
- POS sells only active sellable variants for the selected store.
- Sales include sync metadata, variant ids, payments, tenant id, store id, and device id.
- Offline sales upload successfully and conflicts are visible.
- Product catalog, store assignments, variants, and stock are visually distinct workflows.
- Refunds/voids call backend endpoints and refresh local stock/sales state.
- Cash register and stock operation screens use the backend operational endpoints.
- License activation and usage are visible and enforced.
- Audit logs and sync conflicts are available to roles that can manage them.
- All online-only actions degrade gracefully when offline.
