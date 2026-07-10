# Frontend UI/UX Design And Load Process

This document describes the current Xavissa frontend screen by screen, the design architecture, how data is loaded from the backend into the UI, how it is refreshed, and where speed can be improved.

## 1. Frontend Platform And Design Architecture

The frontend is an Avalonia desktop application using MVVM.

- App entry: `App.axaml` and `App.axaml.cs`
- Main window shell: `MainWindow.axaml`
- Root screen switcher: `AppView.axaml` with `AppViewModel`
- View resolution: `ViewLocator.cs`
- Feature screens: `Views/*View.axaml`
- Screen state and commands: `ViewModels/*ViewModel.cs`
- Data access: repository interfaces plus online/offline implementations under `Data/Repositories`
- Local persistence: SQLite through `LocalDbContext`
- Backend access: named `HttpClient` called `backend`, with auth token injection through `AuthMessageHandler`
- Sync state: `SyncService`, `SessionLoadState`, `BackendHealthService`

The UI uses a central shell pattern:

1. `MainWindow` displays `AppView` and the global snackbar host.
2. `AppViewModel` starts with `LoginViewModel`.
3. After login, `AppViewModel` replaces the current view with `MainViewModel`.
4. `MainViewModel` owns the application shell: top bar, sidebar, current section, store switcher, sync status, user info, logout.
5. Inside `MainView`, a second `ContentControl` swaps feature view models such as Home, Analytics, History, Management, and Config.

The view-to-viewmodel mapping is convention based. `ViewLocator` replaces `.ViewModels.` with `.Views.` and replaces `ViewModel` with `View`, so `HomeViewModel` maps to `HomeView`.

## 2. Visual Design System

The design system is resource driven.

- `BaseTheme.axaml` defines shared semantic colors and brushes.
- `LightTheme.axaml` and `DarkTheme.axaml` override colors for theme mode.
- `MaterialStyles.axaml` defines shared control styles.
- `Localization/Strings.*.axaml` provides localized text resources.

The app has an operational desktop-tool style:

- Neutral background with white or alternate surface panels.
- Green primary action color.
- Compact controls with small corner radii, usually 4-6 px.
- Sidebar navigation with icon paths and active states.
- Data-heavy screens using tables, lists, filter bars, and command buttons.
- Global snackbars for transient success/error/warning messages.
- Session sync banner at the top of the work area when online data is being loaded.

Main reusable visual primitives:

- `Border.panel`: framed work panels.
- `Border.card`: repeated product/stat cards.
- `Border.chip`: small status/filter labels.
- `Button.primary`, `secondary`, `danger`, `ghost`, `nav-item`, `subnav-item`.
- `TextBlock.title` and `TextBlock.caption`.
- Styled `TextBox`, `ComboBox`, `DatePicker`, `TabControl`, and `DataGrid`.

## 3. Page By Page UI/UX

### Login Page

Files:

- `Views/LoginView.axaml`
- `ViewModels/LoginViewModel.cs`

Purpose:

- Entry screen for real login, demo workspace, license activation, backend readiness, and optional store selection.

Main UI responsibilities:

- Username and password fields.
- Password visibility toggle.
- Login command.
- Demo mode command.
- License key activation.
- Backend status message.
- Store selection if the online login returns multiple stores and no store-scoped token yet.
- Busy state with a status message while connecting, opening local workspace, selecting store, or activating license.

Data and refresh behavior:

- On construction, the view model subscribes to `BackendHealthService.Changed`.
- `BackendHealthService` checks `health/connectivity` every 3 seconds after monitoring starts.
- Login first checks whether the backend is ready.
- If ready, `LoginCoordinator` attempts online login against `api/Auth/login`.
- If multiple stores are available, login pauses and asks the user to select a store, then calls `api/Auth/select-store`.
- Successful online login stores the token, caches credentials for store switching, saves a local offline identity, starts the auth session, and navigates to the main shell.
- If the backend is unavailable, login can continue only with a previously cached offline identity.
- After navigation, `SyncOnlineDataAsync` runs in the background and calls `SyncService.SyncAllAsync`.

UX notes:

- The page supports offline continuity after a previous online login.
- Store selection is delayed until required, so users with one store enter the app without extra interaction.
- Demo mode creates a demo workspace and can optionally start a backend demo session if online.

### Main Shell

Files:

- `Views/MainView.axaml`
- `ViewModels/MainViewModel.cs`

Purpose:

- Persistent application frame for authenticated users.

Main UI responsibilities:

- Top bar with app name, demo badge, connectivity status, sync status, date and time.
- Collapsible sidebar with role-aware navigation.
- Management sub-navigation for users, stores, categories, products, and variants.
- Store switcher.
- Current user and role.
- Logout.
- Session loading banner shown while background online data is applied.
- Main content region.

Role-based navigation:

- Tenant admin: analytics, history, management, settings, tenant/store-scoped controls.
- Store manager: analytics, history, management, settings, store-scoped controls.
- Clerk/cashier: sales workspace, history if allowed, settings.
- Unsupported platform/admin roles: routed to an unsupported-role message.
- No assigned tenant/store: routed to a no-workspace message.

Data and refresh behavior:

- Starts a 1-second clock timer for date/time.
- Starts a 5-second connectivity monitor.
- Starts a 3-minute operational refresh timer.
- Watches `SessionLoadState.OnlineDataApplied` after login sync completes.
- Watches auth/user changes and recalculates all navigation permissions.
- Store switching refreshes the auth scope, syncs store-scoped data, resets affected screens, and reloads data views.

Refresh entry points:

- Post-login sync completion.
- Backend reconnect.
- Store switch.
- 3-minute operational refresh.
- Sale creation event.

### Sales Workspace / POS Home

Files:

- `Views/HomeView.axaml`
- `ViewModels/HomeViewModel.cs`

Purpose:

- Main point-of-sale workspace for sellable product selection, cart management, payment, receipt printing, and offline-first sale creation.

Main UI responsibilities:

- Product grid/list.
- Product search.
- Category filter.
- Minimum/maximum price filters.
- Manual barcode entry.
- Barcode scanner input handling.
- Cart list with quantity controls.
- Discount and tendered amount.
- Payment method.
- Subtotal/final total/change.
- Finalize sale.

Data loading:

- `HomeViewModel` loads products when activated.
- `LoadProductsAsync` calls `IProductRepository.GetSellableProductsAsync(selectedStoreId)`.
- The repository returns local SQLite sellable variants as the primary source.
- Product sync/bootstrap populates local sellable variants before or after the screen is visible.
- Search/category/price filtering is done in memory from `_allProducts`.

Sale creation:

- Finalize validates the cart and current stock.
- A local `Sale` with items and payment is created.
- `SaleRepository.CreateAsync` tries online creation when authenticated and online.
- If online creation fails or offline mode is active, the sale is saved locally as unsynced.
- Local stock is decreased immediately.
- Receipt printing is attempted after the sale is stored.
- If online, a background `SyncAfterSaleAsync` runs, then products reload.

UX notes:

- The POS remains usable offline if products and identity were already cached.
- Local stock is reduced immediately for fast user feedback.
- Barcode scanning checks local sellable variants first.

### Sales History

Files:

- `Views/HistoryView.axaml`
- `ViewModels/HistoryViewModel.cs`

Purpose:

- Review previous sales, filter by time/store, inspect sale details, export CSV, delete/refund when permitted.

Main UI responsibilities:

- Sales table/list.
- Filter options: all, today, this week, this month, custom date.
- Tenant admin store filter.
- Totals and average sale amount.
- Load-more pagination.
- Sale details panel/dialog.
- Delete confirmation.
- Refund sale or item dialog.
- CSV export.

Data loading:

- Uses `ISaleRepository.GetHistoryPageAsync(SaleHistoryQuery)`.
- Current page size is 100.
- The repository reads history from local SQLite.
- Online sync updates local SQLite in the background.
- Store names are resolved from auth stores and `IStoreAdminRepository.GetStoresAsync`.

Refresh behavior:

- Reload runs when filter/date/store changes.
- `SaleRepository.SalesChanged` triggers reload.
- Main shell reloads history after login sync, reconnect sync, store switch, and operational refresh.
- Refund/delete require online/authenticated mode, then `SaleRepository.SyncAsync` refreshes local data.

UX notes:

- History is paged, which is good for large sale volumes.
- Refund/delete are intentionally blocked offline.
- CSV export writes to the user's Documents folder.

### Analytics

Files:

- `Views/AnalyticsView.axaml`
- `ViewModels/AnalyticsViewModel.cs`

Purpose:

- Dashboard for tenant/store sales and revenue visibility.

Main UI responsibilities:

- Scope title.
- Store count, product count, sales count, revenue, average sale value.
- Store performance rows.
- Pie chart segments.
- Line chart path, area path, labels, points, and guide lines.

Data loading:

- Tenant admin calls `IAnalyticsRepository.GetTenantAnalyticsAsync(tenantId)`, which hits `api/Analytics/tenant/{tenantId}`.
- Store analytics calls `IAnalyticsRepository.GetStoreAnalyticsAsync()`, which hits `api/Analytics/store`.
- Analytics currently loads from backend endpoints rather than local aggregate calculation.

Refresh behavior:

- Loads once in constructor.
- Reloads on auth user change.
- Reloads on language change.
- Main shell reloads analytics after login sync, reconnect sync, store switch, and operational refresh when the user has analytics access.

UX notes:

- Charts are generated in the view model as path data from returned store rows.
- If the backend call fails, the screen clears analytics collections and shows a localized error notification.

### Management

Files:

- `Views/ManagementView.axaml`
- `ViewModels/ManagementViewModel.cs`

Purpose:

- Role-aware administration workspace for users, stores, categories, catalog products, product assignments, and store variants.

Main UI responsibilities:

- Tabs for team/users, stores, categories, catalog/products, and product variants.
- User filtering/search and bulk status actions.
- Create/edit user flows.
- User-store assignment controls.
- Store creation/editing.
- Category creation/deletion.
- Catalog product creation/editing.
- Product-to-store assignment.
- Store variant creation/editing/deactivation.
- Barcode generation and barcode label printing.

Data loading:

- `LoadCommand` calls `LoadAllAsync`.
- `LoadAllAsync` loads users, catalog, and stores.
- Product/category/store/user data is accessed through repositories.
- Reads generally try online when possible, then update local SQLite, then return local data.
- Selected-user store assignments are loaded when `SelectedUser` changes.
- Selected-product store assignments and variants are loaded when `SelectedProduct` changes.
- Store manager variant lists load variants for products in the selected store.

Refresh behavior:

- Loads on construction through `LoadCommand.Execute().Subscribe()`.
- Reloads on auth user change.
- Main shell reloads management after login sync, reconnect sync, store switch, and operational refresh when management is accessible.
- Mutations generally reload the affected list after success.

UX notes:

- Tenant admin and store manager see different subsets of tabs and actions.
- Several write operations require authenticated online mode.
- Store managers manage sellable variants for their selected store.

### Settings / Config

Files:

- `Views/ConfigView.axaml`
- `ViewModels/ConfigViewModel.cs`

Purpose:

- Printer, receipt, label, theme, language, and license usage settings.

Main UI responsibilities:

- Receipt printer selection.
- Label printer selection.
- Receipt paper, font, header/footer, logo, image position.
- Label dimensions and barcode size.
- Dark/light theme.
- Language selection.
- Test print.
- Apply label defaults.
- License usage summary for tenant admins.

Data loading:

- Loads printer configuration from `IPrinterService`.
- Loads available printers from the operating system through `IPrinterService.AvailablePrinters`.
- Loads license cache from `ILicenseCacheService`.
- If a tenant is selected, loads live usage from `ILicenseClient.GetUsageAsync(tenantId)`, which calls `api/licensing/tenant/{tenantId}/usage`.

Refresh behavior:

- Theme and language changes are applied immediately and persisted through printer configuration.
- License usage can be refreshed by command.
- Scope labels refresh when auth user changes or language changes.

UX notes:

- Preference changes for theme/language auto-save.
- Full printer settings save through the explicit save command.

### No Workspace

Files:

- `Views/NoWorkspaceView.axaml`
- `ViewModels/NoWorkspaceViewModel.cs`

Purpose:

- Empty-state page for a logged-in user with no assigned tenant/store workspace.

UX behavior:

- Shows a centered panel with title, message, and instruction to ask an administrator for assignment.

### Unsupported Role

Files:

- `Views/UnsupportedRoleView.axaml`
- `ViewModels/UnsupportedRoleViewModel.cs`

Purpose:

- Guard page for platform/admin/support roles that are authenticated but not supported by the desktop operational UI.

UX behavior:

- Shows a centered panel and instructs the user to logout.

### PrinterConfigView

Files:

- `Views/PrinterConfigView.axaml`
- `ViewModels/PrinterConvigViewModel.cs`

Purpose:

- Appears to be an older or standalone printer configuration screen.

Current integration note:

- `ConfigViewModel` is registered in DI and exposed through the main Settings navigation.
- `PrinterConvigViewModel` is not registered in the current `App.axaml.cs` DI setup, so the active settings experience is `ConfigView`.

## 4. Backend Load And Sync Architecture

The frontend is offline-first for core POS data.

### Startup

1. `App.OnFrameworkInitializationCompleted` builds the .NET host and registers services, repositories, view models, and the named backend `HttpClient`.
2. Printer preferences are loaded first to set language and theme.
3. `AppViewModel` is resolved and placed into `MainWindow`.
4. Local SQLite schema bootstrap runs in a background task.
5. Backend process startup runs in a background task.
6. Backend health monitoring begins and checks `health/connectivity`.

This keeps the critical UI startup path short: the login screen can appear while local schema and backend readiness continue in the background.

### Authentication

1. `LoginViewModel.LoginAsync` validates inputs.
2. `LoginCoordinator.LoginAsync` ensures the local schema exists.
3. `BackendHealthService.CheckAsync` determines whether online login should be attempted.
4. Online login uses `AuthRepositoryOnline.LoginAsync` against `api/Auth/login`.
5. Store selection may call `api/Auth/select-store`.
6. Token is saved in `ApiTokenProvider`.
7. Login response is saved to local identity storage.
8. `AuthService.StartSession` establishes current user/role/tenant/store state.
9. App navigates to `MainViewModel`.
10. Background sync starts.

Offline login:

- If the backend is unavailable, `LoginCoordinator` calls `LocalIdentityService.ValidateOfflineLoginAsync`.
- Offline mode depends on a previously cached identity.
- Backend health is marked as offline cached mode.

### Repository Pattern

Most core repositories have online and offline sides:

- `ProductRepositoryOnline` talks to backend product endpoints.
- `ProductRepositoryOffline` talks to SQLite.
- `ProductRepository` coordinates both.
- `SaleRepositoryOnline` talks to backend sales endpoints.
- `SaleRepositoryOffline` talks to SQLite.
- `SaleRepository` coordinates both.
- `UserRepositoryOnline` talks to backend users endpoints.
- `UserRepositoryOffline` talks to cached identities.
- `UserRepository` coordinates both.

Common read strategy:

1. If authenticated online is available, try backend.
2. Merge backend result into local SQLite.
3. Return local SQLite data to the UI.
4. If backend fails, return local SQLite fallback.

Common write strategy:

- Sales are offline-first and can be stored locally for later upload.
- Administrative writes usually require authenticated online mode.
- Product/category/store/variant management mostly writes online, then reloads affected local data.

### Sync Service

`SyncService` owns explicit sync flows:

- `SyncAllAsync`
- `SyncStoreScopedDataAsync`
- `SyncUsersAsync`
- `SyncProductsAsync`
- `SyncSalesAsync`
- `RefreshOperationalDataAsync`
- `SyncAfterReconnectAsync`
- `SyncAfterSaleAsync`

Important backend sync endpoints:

- `api/sync/bootstrap?includeCatalog=true`
- `api/sync/bootstrap`
- `api/sync/catalog?tenantId=...&updatedAfter=...`
- `api/sync/sellable-variants?storeId=...&updatedAfter=...`
- `api/sync/stock-levels?storeId=...&updatedAfter=...`
- `api/sync/sales/upload`
- `api/sync/sales?storeId=...&updatedAfter=...`

Cursor keys:

- `catalog:{tenantId}`
- `sellable:{storeId}`
- `stock:{storeId}`
- `sales:{storeId}`

The cursor model means the app should pull only changed catalog, sellable variant, stock, and sales data after the initial bootstrap.

### Refresh Lifecycle

Post-login refresh:

1. Login navigates to the main shell quickly.
2. Background task shows session load state.
3. `SyncService.SyncAllAsync` uploads pending sales, syncs users, stores, bootstrap data, and sales.
4. `SessionLoadState.NotifyOnlineDataApplied` tells the main shell to reload screens.
5. `MainViewModel.ReloadDataViewsAsync` reloads products, history, analytics, and management if allowed.

Reconnect refresh:

1. Connectivity timer detects offline-to-online transition.
2. `SyncAfterReconnectAsync` uploads pending sales and syncs store-scoped data.
3. Data views reload.

Operational refresh:

1. Every 3 minutes, if online and a store is selected, `RefreshOperationalDataAsync` runs.
2. It syncs sales, sellable variants, and stock levels.
3. Data views reload.

Store switch refresh:

1. `SelectedStoreId` setter calls `SwitchSelectedStoreAsync`.
2. If online, backend issues a store-scoped token.
3. If offline, cached identity store scope is updated when possible.
4. Home, History, and Analytics are reset.
5. Store-scoped sync runs.
6. Data views reload.

Sale refresh:

1. A sale is created online or locally.
2. Local stock is reduced immediately.
3. `SalesChanged` reloads history.
4. If online, post-sale sync updates sales and stock, then products reload.

## 5. Performance Improvement Suggestions

### High Impact

1. Parallelize independent reloads.

`MainViewModel.ReloadDataViewsAsync` currently reloads Home, History, Analytics, and Management sequentially. Where dependencies allow, run independent loads with `Task.WhenAll`, especially analytics and management. Keep UI collection updates on the UI thread, but overlap backend/local IO.

2. Parallelize management initial loading.

`ManagementViewModel.LoadAllAsync` loads users, catalog, and stores sequentially. These are mostly independent and can be loaded in parallel, then applied to the UI after all results return.

3. Reduce N+1 variant loading.

Management variant loading loops over products and calls `GetVariantsAsync` for each product. For many products this creates many backend/local queries. Add a backend endpoint and offline query for all variants by store, or all variants for a product batch.

4. Avoid full view reload after every sync.

After operational refresh, the app reloads products, history, analytics, and management. Use sync result metadata to reload only changed areas. For example, stock-only refresh should usually reload Home products, not users/stores/categories.

5. Cache store names for history.

`HistoryViewModel.LoadSalesPageAsync` resolves stores every page. Cache store lookup per session/store switch, and invalidate it only after store management changes or reconnect sync.

### Medium Impact

6. Debounce product filters.

`HomeViewModel.SearchText`, price filters, and category filters apply immediately on every setter. Add a short debounce for text/price input so large product lists are not filtered on every keystroke.

7. Batch observable collection updates.

Several screens clear and repopulate `ObservableCollection` item by item. For large lists this can cause many UI change notifications. Use a replace-range collection, temporary list binding, or suspend notifications where possible.

8. Add SQLite indexes for common local reads.

Useful indexes include:

- Sellable variants by `StoreId`, `Barcode`, `UpdatedAt`
- Products by `OnlineId`, `VariantId`, `StoreId`, `Name`
- Sales by `StoreId`, `Timestamp`, `UpdatedAt`, `Synced`
- Sale items by `SaleId`, `VariantId`
- Sync cursors by key

9. Move heavy chart calculations off the UI update path.

Analytics builds chart path strings after data load. This is fine for small store counts, but for larger data sets calculate chart geometry on a background thread, then apply the final collections/path strings to the UI.

10. Use backend pagination/filtering for large catalog and user management lists.

Catalog and user screens currently tend to load broad lists. For large tenants, add backend query parameters for search, status, store, page, and page size, then keep local cache in sync.

### Lower Risk Polish

11. Use cancellation tokens for refreshes.

When users switch stores or filters quickly, older reloads can finish late and apply stale UI state. Add cancellation for Home/History/Analytics/Management reloads.

12. Avoid duplicate startup monitoring calls.

Backend health monitoring can be started from startup and login construction. It is guarded, but keeping ownership centralized would make the startup sequence easier to reason about.

13. Make `ConnectivityService.IsOnline` cheap or cached.

If `IsOnline()` performs network work, it is called often from timers and repositories. Prefer a cached state updated by health/connectivity monitoring, with explicit checks only where needed.

14. Keep local SQLite warm.

The app already warms local schema in the background. Similar warm-ups could prepare frequently used SQLite queries or load the current store's sellable variants shortly after login.

15. Compile bindings consistently.

Most views use `x:DataType`. Ensure compiled bindings are enabled consistently where possible and remove `x:CompileBindings="False"` from legacy screens if they become active.

## 6. Suggested Next Design Improvements

- Add skeleton/loading states per screen rather than relying only on a global session banner.
- Add empty states for no products, no history, no analytics data, and no management records.
- Make sync status more specific: "Uploading sales", "Updating stock", "Catalog updated", "Offline cache".
- Add last sync time in the shell so users can trust offline data freshness.
- Improve POS list performance with virtualization if product counts become large.
- Separate tenant-admin and store-manager management workflows more clearly so each tab shows only immediately relevant actions.
- Add user-facing conflict messages for sync conflicts instead of silently logging most failures.

